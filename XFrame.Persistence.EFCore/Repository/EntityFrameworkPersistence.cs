using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections;
using XFrame.Common;
using XFrame.Common.Exceptions;
using XFrame.Common.Extensions;
using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore
{
    public class EntityFrameworkPersistence<TDbContext> : IPersistence
        where TDbContext : DbContext
    {
        private readonly ILogger<EntityFrameworkPersistence<TDbContext>> _logger;
        private readonly IUniqueConstraintDetectionStrategy _strategy;
        private readonly TDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly AsyncLock _asyncLock = new AsyncLock();

        private bool _disposed;

        public EntityFrameworkPersistence(
            ILogger<EntityFrameworkPersistence<TDbContext>> logger,
            IDbContextProvider<TDbContext> contextProvider,
            IUniqueConstraintDetectionStrategy strategy,
            IConfiguration configuration)
        {
            _logger = logger;
            _context = contextProvider.CreateContext();
            _strategy = strategy;
            _configuration = configuration;
        }

        public async Task Dispose(CancellationToken cancellationToken)
        {
            if (_disposed)
                return;

            using (await _asyncLock.WaitAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                if (_disposed)
                    return;

                _context.Dispose();
                _disposed = true;
            }
        }

        public async Task<TDomain> Get<TDomain, TDomainCriteria>(
            TDomainCriteria domainCriteria,
            CancellationToken cancellationToken)
            where TDomain : class
            where TDomainCriteria : DomainCriteria
        {
            using (await _asyncLock.WaitAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                ThrowIfDisposed();

                return await EntityFrameworkExpressionBuilder
                    .BuildCriteria(
                        _context.Set<TDomain>(),
                        domainCriteria)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }


        public async Task<IList<TDomain>> GetAll<TDomain, TDomainCriteria>(
            TDomainCriteria domainCriteria,
            CancellationToken cancellationToken)
            where TDomain : class
            where TDomainCriteria : DomainCriteria
        {
            using (await _asyncLock.WaitAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                ThrowIfDisposed();

                return await EntityFrameworkExpressionBuilder
                    .BuildCriteria(
                        _context.Set<TDomain>(),
                        domainCriteria)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }


        public async Task Save<TDomain>(
            TDomain domain,
            CancellationToken cancellationToken)
            where TDomain : class
        {
            using (await _asyncLock.WaitAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                ThrowIfDisposed();

                await ExecuteWriteAsync(
                    domain,
                    "save",
                    async () =>
                    {
                        _context.Add(domain);

                        await _context.SaveChangesAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task Update<TDomain>(
            TDomain domain,
            CancellationToken cancellationToken)
            where TDomain : class
        {
            using (await _asyncLock.WaitAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                ThrowIfDisposed();

                await ExecuteWriteAsync(
                    domain,
                    "update",
                    async () =>
                    {
                        UpdateEntityGraph(domain);

                        await _context.SaveChangesAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task Delete<TDomain>(
            TDomain domain,
            CancellationToken cancellationToken)
            where TDomain : class
        {
            using (await _asyncLock.WaitAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                ThrowIfDisposed();

                await ExecuteWriteAsync(
                    domain,
                    "delete",
                    async () =>
                    {
                        _context.Remove(domain);

                        await _context.SaveChangesAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<TModel>> ExecuteStoredProcedure<TModel>(
            NamedCriteria namedCriteria,
            CancellationToken cancellationToken)
        {
            using (await _asyncLock.WaitAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                ThrowIfDisposed();

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var timeout = int.TryParse(
                        _configuration["Timeout"],
                        out var configuredTimeout)
                            ? configuredTimeout
                            : 180;

                    if (timeout <= 0)
                        timeout = 180;

                    var result = await _context
                        .GetStoredProcedure(namedCriteria.Name)
                        .WithSqlParams(namedCriteria.Parameters)
                        .ExecuteStoredProc<TModel>(timeout)
                        .ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to execute stored procedure {ProcedureName}",
                        namedCriteria.Name);

                    throw;
                }
            }
        }

        private void UpdateEntityGraph<TDomain>(TDomain domain)
            where TDomain : class
        {
            _context.ChangeTracker.TrackGraph(domain, node =>
            {
                var entry = node.Entry;
                var primaryKey = entry.Metadata.FindPrimaryKey();

                if (primaryKey == null)
                {
                    throw new InvalidOperationException(
                        $"Entity {entry.Metadata.ClrType.Name} has no primary key.");
                }

                var keyValues = primaryKey.Properties
                    .Select(property =>
                        entry.Property(property.Name).CurrentValue)
                    .ToArray();

                var existingEntry = _context.ChangeTracker.Entries()
                    .FirstOrDefault(trackedEntry =>
                        trackedEntry.Metadata == entry.Metadata &&
                        primaryKey.Properties.Select((property, index) =>
                            Equals(trackedEntry.Property(property.Name).CurrentValue,
                                keyValues[index])).All(matches => matches));

                var existingEntity = existingEntry?.Entity ?? _context.Find(
                    entry.Metadata.ClrType,
                    keyValues);

                if (existingEntity == null)
                {
                    entry.State = EntityState.Added;
                    return;
                }

                if (ReferenceEquals(existingEntity, entry.Entity))
                {
                    entry.State = EntityState.Modified;
                    return;
                }

                var trackedEntityEntry = existingEntry ?? _context.Entry(existingEntity);
                trackedEntityEntry.CurrentValues.SetValues(entry.Entity);
                ReplaceGraphReference(node, existingEntity);

                trackedEntityEntry.State = EntityState.Modified;
            });
        }

        private static void ReplaceGraphReference(
            EntityEntryGraphNode node,
            object trackedEntity)
        {
            if (node.SourceEntry == null || node.InboundNavigation == null)
            {
                return;
            }

            var navigation = node.SourceEntry.Navigation(node.InboundNavigation.Name);
            if (!node.InboundNavigation.IsCollection)
            {
                navigation.CurrentValue = trackedEntity;
                return;
            }

            var collection = navigation.CurrentValue;
            if (collection is IList list)
            {
                var index = list.IndexOf(node.Entry.Entity);
                if (index >= 0)
                {
                    list[index] = trackedEntity;
                }
                return;
            }

            var collectionInterface = collection?.GetType().GetInterfaces()
                .FirstOrDefault(type => type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(ICollection<>));
            if (collection == null || collectionInterface == null)
            {
                return;
            }

            collectionInterface.GetMethod(nameof(ICollection<object>.Remove))!
                .Invoke(collection, new[] { node.Entry.Entity });
            collectionInterface.GetMethod(nameof(ICollection<object>.Add))!
                .Invoke(collection, new[] { trackedEntity });
        }


        private async Task ExecuteWriteAsync<TDomain>(
            TDomain domain,
            string operation,
            Func<Task> action)
            where TDomain : class
        {
            var entityId = GetEntityId(domain);

            try
            {
                await action().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Optimistic concurrency conflict during {Operation} " +
                    "for entity {EntityType} with ID {EntityId}",
                    operation,
                    typeof(TDomain).Name,
                    entityId);

                throw new OptimisticConcurrencyException(
                    ex.Message,
                    ex);
            }
            catch (DbUpdateException ex)
                when (ex.IsUniqueConstraintViolation(_strategy))
            {
                _logger.LogWarning(
                    ex,
                    "Unique constraint violation during {Operation} " +
                    "for entity {EntityType} with ID {EntityId}",
                    operation,
                    typeof(TDomain).Name,
                    entityId);

                throw new OptimisticConcurrencyException(
                    ex.Message,
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Entity Framework {Operation} failed for " +
                    "entity {EntityType} with ID {EntityId}",
                    operation,
                    typeof(TDomain).Name,
                    entityId);

                throw;
            }
        }


        private static object GetEntityId<TDomain>(TDomain domain)
        {
            try
            {
                return domain.GetType()
                    .GetProperty("Id")?
                    .GetValue(domain) ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(EntityFrameworkPersistence<TDbContext>));
            }
        }
    }
}

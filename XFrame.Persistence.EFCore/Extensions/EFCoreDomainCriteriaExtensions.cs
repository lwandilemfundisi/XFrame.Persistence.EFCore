using XFrame.Common.Extensions;
using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.EFCore.Queries.Filtering.Builders;
using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Extensions;

public static class EFCoreDomainCriteriaExtensions
{
    /// <summary>Appends strongly typed, expression-selected conditions to the criteria using AND semantics.</summary>
    public static EFCoreDomainCriteria BuildCriteria<TEntity>(
        this EFCoreDomainCriteria domainCriteria,
        Action<EntityCriteriaBuilder<TEntity>> configure) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(domainCriteria);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new EntityCriteriaBuilder<TEntity>();
        configure(builder);
        var filter = builder.Build();
        if (filter is not null) Append(domainCriteria, filter);
        return domainCriteria;
    }

    /// <summary>Appends the generated filter to the criteria using AND semantics.</summary>
    public static EFCoreDomainCriteria BuildCriteria(
        this EFCoreDomainCriteria domainCriteria,
        Action<DomainCriteriaBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(domainCriteria);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new DomainCriteriaBuilder();
        configure(builder);
        var filter = builder.Build();

        if (filter is not null)
            Append(domainCriteria, filter);

        return domainCriteria;
    }

    /// <summary>Appends an existing filter to the criteria using AND semantics.</summary>
    public static EFCoreDomainCriteria And(
        this EFCoreDomainCriteria domainCriteria,
        BaseFilter filter)
    {
        ArgumentNullException.ThrowIfNull(domainCriteria);
        ArgumentNullException.ThrowIfNull(filter);
        Append(domainCriteria, filter);
        return domainCriteria;
    }

    private static void Append(EFCoreDomainCriteria criteria, BaseFilter filter)
        => criteria.Filter = criteria.Filter is null ? filter : new AndFilter(criteria.Filter, filter);
}

# XFrame.Persistence.EFCore

Entity Framework Core persistence implementation for the XFrame persistence abstractions.

The package provides an EF Core-backed persistence implementation, criteria-based querying, dynamic filter translation, sorting and navigation inclusion, stored-procedure execution, batched deletion helpers, and value-object conversions. It also includes a fluent API for composing dynamic filter trees with conditional clauses and nested `AND`, `OR`, and `NOT` groups.

## Contents

- [Installation](#installation)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Persistence operations](#persistence-operations)
- [Criteria-based queries](#criteria-based-queries)
- [Fluent filtering](#fluent-filtering)
- [Sorting and including navigation properties](#sorting-and-including-navigation-properties)
- [Stored procedures](#stored-procedures)
- [Batch deletion](#batch-deletion)
- [Value-object conversions](#value-object-conversions)
- [EF Core service configuration](#ef-core-service-configuration)
- [How filtering is translated](#how-filtering-is-translated)
- [Important notes and limitations](#important-notes-and-limitations)

## Installation

```bash
dotnet add package XFrame.Persistence.EFCore
```

The project currently targets **.NET 8** and **.NET 9**. It references `Microsoft.EntityFrameworkCore` 9.0.1, `Microsoft.EntityFrameworkCore.Relational` 9.0.1, `XFrame.Persistence` 9.0.0, and `XFrame.Ids` 9.0.0. Add a compatible EF Core database-provider package (such as the SQL Server provider) to your application.

## Requirements

Your application supplies an EF Core `DbContext`, an implementation of `IDbContextProvider<TDbContext>`, an EF Core provider, and the normal XFrame persistence registrations.

A minimal context provider:

```csharp
using Microsoft.EntityFrameworkCore;
using XFrame.Persistence.EFCore;

public sealed class ApplicationDbContextProvider
    : IDbContextProvider<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ApplicationDbContextProvider(
        DbContextOptions<ApplicationDbContext> options)
    {
        _options = options;
    }

    public ApplicationDbContext CreateContext()
        => new ApplicationDbContext(_options);
}
```

Register the context options, provider, and XFrame persistence services according to your application's composition conventions.

## Getting started

`EntityFrameworkPersistence<TDbContext>` implements the XFrame `IPersistence` abstraction. It obtains its context from `IDbContextProvider<TDbContext>` and exposes these operations through `IPersistence`:

| Operation | Behaviour |
|---|---|
| `Get<TDomain, TCriteria>` | Returns the first entity matching the criteria, or the default value if none matches. |
| `GetAll<TDomain, TCriteria>` | Returns all entities matching the criteria. |
| `Save<TDomain>` | Adds an entity and saves changes. |
| `Update<TDomain>` | Tracks and updates an entity graph, then saves changes. |
| `Delete<TDomain>` | Removes an entity and saves changes. |
| `ExecuteStoredProcedure<TModel>` | Executes a named stored procedure and maps result rows to the requested model. |

Example calls (assuming `persistence` is an `IPersistence` instance):

```csharp
await persistence.Save(order, cancellationToken);
await persistence.Update(order, cancellationToken);
await persistence.Delete(order, cancellationToken);

var order = await persistence.Get<Order, OrderCriteria>(criteria, cancellationToken);
var orders = await persistence.GetAll<Order, OrderCriteria>(criteria, cancellationToken);
```

The persistence instance serializes access to its internally-created `DbContext` using an asynchronous lock. Dispose it through its `Dispose` method when its lifetime is complete.

## Criteria-based queries

Derive a criteria type from `EFCoreCriteriaDomainQuery<TDomain>` and override `OnBuildDomainCriteria`:

```csharp
using XFrame.Persistence.EFCore.Queries.CriteriaQueries;
using XFrame.Persistence.EFCore.Queries.Filtering;

public sealed class OrderCriteria : EFCoreCriteriaDomainQuery<Order>
{
    public IReadOnlyCollection<OrderStatus> Statuses { get; init; } = [];
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }

    protected override void OnBuildDomainCriteria(EFCoreDomainCriteria domainCriteria)
    {
        domainCriteria.BuildCriteria(criteria => criteria
            .When(Statuses.Count > 0,
                x => x.In(nameof(Order.Status), Statuses))
            .When(CreatedFrom.HasValue,
                x => x.GreaterThanOrEqual(nameof(Order.CreatedDate), CreatedFrom!.Value))
            .When(CreatedTo.HasValue,
                x => x.LessThanOrEqual(nameof(Order.CreatedDate), CreatedTo!.Value)));
    }
}
```

`BuildCriteria` appends its generated filter to any existing criteria filter using `AND` semantics. If no conditions are added, the existing criteria is unchanged.

## Fluent filtering

Namespaces:

```csharp
using XFrame.Persistence.EFCore.Extensions;
using XFrame.Persistence.EFCore.Queries.Filtering.Builders;
```

`BuildCriteria` creates a filter tree; it does not execute a query. The EF Core persistence implementation translates that tree when the criteria is executed.

### Basic conditions

```csharp
domainCriteria.BuildCriteria(criteria => criteria
    .Equal(nameof(Order.IsActive), true)
    .NotEqual(nameof(Order.Status), OrderStatus.Cancelled)
    .GreaterThan(nameof(Order.Total), 1000m)
    .LessThanOrEqual(nameof(Order.CreatedDate), DateTime.Today));
```

All top-level conditions are combined with `AND`:

```text
IsActive = true
AND Status <> Cancelled
AND Total > 1000
AND CreatedDate <= Today
```

### Conditional conditions

`When` and `Unless` add conditions only when their Boolean condition is satisfied:

```csharp
domainCriteria.BuildCriteria(criteria => criteria
    .When(!string.IsNullOrWhiteSpace(customerName),
        x => x.Contains(nameof(Order.CustomerName), customerName!))
    .When(statuses.Count > 0,
        x => x.In(nameof(Order.Status), statuses))
    .When(minimumTotal.HasValue,
        x => x.GreaterThanOrEqual(nameof(Order.Total), minimumTotal!.Value))
    .Unless(includeArchived,
        x => x.Equal(nameof(Order.IsArchived), false)));
```

If the condition is false, the callback is not executed and no filter is added.

### Nested AND and OR groups

Use `And` and `Or` to create nested groups:

```csharp
domainCriteria.BuildCriteria(criteria => criteria
    .Equal(nameof(Customer.IsActive), true)
    .Or(location => location
        .Equal(nameof(Customer.City), "Cape Town")
        .Equal(nameof(Customer.City), "Johannesburg"))
    .And(spending => spending
        .GreaterThan(nameof(Customer.TotalSpent), 10_000m)
        .LessThan(nameof(Customer.TotalSpent), 100_000m)));
```

Equivalent logic:

```text
IsActive = true
AND (City = 'Cape Town' OR City = 'Johannesburg')
AND (TotalSpent > 10000 AND TotalSpent < 100000)
```

Groups can be nested to represent more complex rules:

```csharp
domainCriteria.BuildCriteria(criteria => criteria
    .Equal(nameof(Order.IsDeleted), false)
    .Or(group => group
        .And(premium => premium
            .Equal(nameof(Order.CustomerType), CustomerType.Premium)
            .GreaterThan(nameof(Order.Total), 5000m))
        .And(standard => standard
            .Equal(nameof(Order.CustomerType), CustomerType.Standard)
            .GreaterThan(nameof(Order.Total), 10_000m))));
```

Equivalent logic:

```text
IsDeleted = false
AND (
    (CustomerType = Premium AND Total > 5000)
    OR
    (CustomerType = Standard AND Total > 10000)
)
```

### NOT groups

`Not` negates the filter tree created inside its callback:

```csharp
domainCriteria.BuildCriteria(criteria => criteria
    .Equal(nameof(User.IsActive), true)
    .Not(excluded => excluded
        .Or(group => group
            .Equal(nameof(User.Status), UserStatus.Suspended)
            .Equal(nameof(User.Status), UserStatus.Archived))));
```

Equivalent logic:

```text
IsActive = true
AND NOT (Status = Suspended OR Status = Archived)
```

### Using existing filter objects

Use `Where` to add an existing `BaseFilter`:

```csharp
domainCriteria.BuildCriteria(criteria => criteria
    .Equal(nameof(Order.IsActive), true)
    .Where(new EqualityFilter(
        nameof(Order.Status), excludedStatuses, FilterType.NotIn)));
```

Or append an existing filter directly:

```csharp
domainCriteria.And(existingFilter);
```

Both extension methods append with `AND` semantics and preserve the existing filter tree.

### Supported operators

| Builder method | Meaning | Example |
|---|---|---|
| `Equal(property, value)` | Equality | `Equal("Status", status)` |
| `NotEqual(property, value)` | Inequality | `NotEqual("Status", status)` |
| `In(property, values)` | Membership | `In("Status", statuses)` |
| `NotIn(property, values)` | Non-membership | `NotIn("Status", excluded)` |
| `GreaterThan(property, value)` | `>` | `GreaterThan("Total", 100)` |
| `GreaterThanOrEqual(property, value)` | `>=` | `GreaterThanOrEqual("Total", 100)` |
| `LessThan(property, value)` | `<` | `LessThan("Total", 500)` |
| `LessThanOrEqual(property, value)` | `<=` | `LessThanOrEqual("Total", 500)` |
| `Contains(property, value)` | String contains | `Contains("Name", "sam")` |
| `StartsWith(property, value)` | String starts with | `StartsWith("Name", "Sam")` |
| `IsNull(property)` | Null check | `IsNull("DeletedAt")` |
| `IsNotNull(property)` | Not-null check | `IsNotNull("CustomerId")` |
| `IsNotEmpty(property)` | Non-null, non-empty string | `IsNotEmpty("Name")` |
| `Where(filter)` | Existing filter node | `Where(existingFilter)` |

## Sorting and including navigation properties

`EFCoreDomainCriteria` adds an EF Core `Include` property to the XFrame `DomainCriteria`:

```csharp
protected override void OnBuildDomainCriteria(EFCoreDomainCriteria domainCriteria)
{
    domainCriteria.Include = new Include(nameof(Order.Customer));
    domainCriteria.BuildCriteria(criteria => criteria
        .Equal(nameof(Order.IsDeleted), false));
}
```

Sorting is configured using the `SortOrder` property inherited from `DomainCriteria` and its `SortOrderType`, as defined by `XFrame.Persistence`. The current implementation applies one configured include and one configured sort order per criteria.

## Stored procedures

The package includes `EFCoreNamedQuery<TModel>`, `EFCoreNamedQueryHandler<TModel>`, and `EFCoreNamedCriteria`, along with `DbContextExtensions` helpers for stored-procedure commands, parameters, execution, and result mapping.

`EFCoreNamedCriteria` carries the stored-procedure name and parameter dictionary inherited from the XFrame abstractions. The persistence implementation reads the optional `Timeout` configuration value; if missing or invalid, execution defaults to 180 seconds.

Use `[MapTo]` to map a model property to a differently named result column:

```csharp
using XFrame.Persistence.EFCore.Attributes;

public sealed class CustomerSummary
{
    [MapTo("customer_name")]
    public string Name { get; set; } = string.Empty;
}
```

Result columns are matched case-insensitively. Without `[MapTo]`, the property name is used.

## Batch deletion

`DbContextExtensions.Delete` repeatedly creates a context, selects a batch, deletes it, and continues until no records remain or cancellation is requested. It accepts a context provider, batch size, cancellation token, projection, optional filter expression, and optional callback to set entity properties before deletion.

```csharp
var deleted = await DbContextExtensions.Delete<
    ApplicationDbContext, AuditRecord, AuditRecord>(
        contextProvider,
        batchSize: 500,
        cancellationToken,
        record => record,
        record => record.CreatedDate < retentionCutoff);
```

Choose a batch size appropriate for the database and workload. A new context is created and disposed for each batch.

## Value-object conversions

The package contains `ValueObjectValueConverter` and `IdentityValueConverter` for mapping XFrame value-object and identity types to values supported by EF Core. Apply the converters in your EF Core model configuration where those types are used.

## EF Core service configuration

`EntityFrameworkConfiguration` configures two extension points: `IUniqueConstraintDetectionStrategy` and `IBulkOperationConfiguration`. The defaults are `DefaultUniqueConstraintDetectionStrategy` and `DefaultBulkOperationConfiguration` (default deletion batch size: 1000).

Replace either implementation as needed:

```csharp
var configuration = EntityFrameworkConfiguration.New
    .UseUniqueConstraintDetectionStrategy<MyUniqueConstraintStrategy>()
    .UseBulkOperationConfiguration<MyBulkOperationConfiguration>();

configuration.Apply(services);
```

The custom strategy must implement `IUniqueConstraintDetectionStrategy`; the custom bulk configuration must implement `IBulkOperationConfiguration`.

## How filtering is translated

The fluent builder creates filter nodes, not raw SQL. The EF Core expression builder translates supported nodes into LINQ expressions:

- `LogicalFilter` becomes `AndAlso`, `OrElse`, or `Not`.
- Existing `AndFilter` becomes `AndAlso`.
- `EqualityFilter` becomes a comparison, string operation, or collection-membership expression.
- Existing `NullFilter` becomes a null check.

The expression is applied as a `Where` clause and EF Core translates the LINQ query for the configured provider. Dotted property paths are supported for nested reference properties, for example:

```csharp
criteria.Equal("Customer.Address.City", "Cape Town");
```

The path must be valid for the queried entity and translatable by the provider.

## Important notes and limitations

- Top-level builder conditions are combined with `AND`; use an `Or` group when OR semantics are required.
- `BuildCriteria` appends to the existing criteria filter with `AND`; it does not replace it.
- An empty builder leaves the criteria unchanged.
- A `Not` callback must create one filter tree, which is then negated.
- `Contains` and `StartsWith` require string properties and non-null string values. Database collation determines comparison case sensitivity.
- `In` and `NotIn` require a non-string enumerable. Values are materialized when the builder method is called. Large collections can encounter provider parameter limits or performance issues.
- `Equal(property, null)` and `IsNull(property)` generate null checks. `NotEqual(property, null)` and `IsNotNull(property)` generate not-null checks.
- Dotted property paths support nested reference-property access. Collection navigation predicates such as `Any` are not currently exposed by the fluent builder.
- The current criteria implementation exposes one `Include` property and applies one include expression.
- The current expression builder applies one configured sort property and direction.
- Unknown filter nodes and unsupported operators throw rather than being silently ignored.
- A constructible expression is not guaranteed to be translatable by every EF Core provider. Test against the provider used by your application.
- Build the solution and run provider-backed integration tests against the exact `XFrame.Persistence` dependency version before publishing.

## Contributing

Contributions should preserve compatibility with the existing XFrame persistence abstractions and include tests for new filter nodes, nested logical groups, and EF Core provider translation.

## License

Add the repository's applicable license information here before publishing or redistributing the package.

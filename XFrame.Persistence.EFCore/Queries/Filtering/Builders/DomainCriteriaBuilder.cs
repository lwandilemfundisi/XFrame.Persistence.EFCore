using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering.Builders;

/// <summary>Builds a filter tree using a fluent API.</summary>
public sealed class DomainCriteriaBuilder
{
    private readonly List<BaseFilter> _filters = new();

    public DomainCriteriaBuilder When(bool condition, Action<DomainCriteriaBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (condition) configure(this);
        return this;
    }

    public DomainCriteriaBuilder Unless(bool condition, Action<DomainCriteriaBuilder> configure)
        => When(!condition, configure);

    public DomainCriteriaBuilder Where(BaseFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filters.Add(filter);
        return this;
    }

    public DomainCriteriaBuilder Equal(string property, object? value) => Compare(property, value, FilterType.Equal);
    public DomainCriteriaBuilder NotEqual(string property, object? value) => Compare(property, value, FilterType.NotEqual);
    public DomainCriteriaBuilder In<T>(string property, IEnumerable<T> values) => Compare(property, Materialize(values), FilterType.In);
    public DomainCriteriaBuilder NotIn<T>(string property, IEnumerable<T> values) => Compare(property, Materialize(values), FilterType.NotIn);
    public DomainCriteriaBuilder GreaterThan(string property, object value) => Compare(property, value, FilterType.GreaterThan);
    public DomainCriteriaBuilder GreaterThanOrEqual(string property, object value) => Compare(property, value, FilterType.GreaterThanOrEqualTo);
    public DomainCriteriaBuilder LessThan(string property, object value) => Compare(property, value, FilterType.LessThan);
    public DomainCriteriaBuilder LessThanOrEqual(string property, object value) => Compare(property, value, FilterType.LessThanOrEqualTo);
    public DomainCriteriaBuilder Contains(string property, string value) => Compare(property, value, FilterType.Like);
    public DomainCriteriaBuilder StartsWith(string property, string value) => Compare(property, value, FilterType.StartsWith);
    public DomainCriteriaBuilder IsNull(string property) => Compare(property, null, FilterType.Null);
    public DomainCriteriaBuilder IsNotNull(string property) => Compare(property, null, FilterType.NotNull);
    public DomainCriteriaBuilder IsNotEmpty(string property) => Compare(property, null, FilterType.NotEmpty);

    public DomainCriteriaBuilder And(Action<DomainCriteriaBuilder> configure) => AddGroup(FilterLogicalOperator.And, configure);
    public DomainCriteriaBuilder Or(Action<DomainCriteriaBuilder> configure) => AddGroup(FilterLogicalOperator.Or, configure);
    public DomainCriteriaBuilder Not(Action<DomainCriteriaBuilder> configure) => AddGroup(FilterLogicalOperator.Not, configure);

    /// <summary>Builds all top-level conditions as an AND group.</summary>
    public BaseFilter? Build() => Build(FilterLogicalOperator.And);

    private BaseFilter? Build(FilterLogicalOperator logicalOperator)
    {
        if (_filters.Count == 0) return null;
        if (_filters.Count == 1) return _filters[0];
        return new LogicalFilter(logicalOperator, _filters.ToArray());
    }

    private DomainCriteriaBuilder AddGroup(FilterLogicalOperator logicalOperator, Action<DomainCriteriaBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var nested = new DomainCriteriaBuilder();
        configure(nested);
        var filter = logicalOperator == FilterLogicalOperator.Not
            ? nested.Build()
            : nested.Build(logicalOperator);
        if (filter is null) return this;
        _filters.Add(logicalOperator == FilterLogicalOperator.Not
            ? new LogicalFilter(FilterLogicalOperator.Not, filter)
            : filter);
        return this;
    }

    private DomainCriteriaBuilder Compare(string property, object? value, FilterType filterType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        _filters.Add(new EqualityFilter(property, value, filterType));
        return this;
    }

    private static T[] Materialize<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values as T[] ?? values.ToArray();
    }
}

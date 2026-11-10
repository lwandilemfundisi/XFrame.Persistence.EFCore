using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering;

/// <summary>Logical operator used to combine child filters.</summary>
public enum FilterLogicalOperator
{
    And,
    Or,
    Not
}

/// <summary>A composable logical filter node for EF Core criteria.</summary>
public sealed class LogicalFilter : BaseFilter
{
    public LogicalFilter(FilterLogicalOperator @operator, params BaseFilter[] filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Length == 0)
            throw new ArgumentException("A logical filter must contain at least one child filter.", nameof(filters));
        if (@operator == FilterLogicalOperator.Not && filters.Length != 1)
            throw new ArgumentException("A NOT filter must contain exactly one child filter.", nameof(filters));
        if (filters.Any(filter => filter is null))
            throw new ArgumentException("A logical filter cannot contain null child filters.", nameof(filters));

        Operator = @operator;
        Filters = Array.AsReadOnly(filters.ToArray());
    }

    public FilterLogicalOperator Operator { get; }
    public IReadOnlyList<BaseFilter> Filters { get; }
}

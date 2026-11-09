using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering;

public enum FilterLogicalOperator
{
    And,
    Or,
    Not
}

public sealed class LogicalFilter : BaseFilter
{
    public LogicalFilter(FilterLogicalOperator @operator, params BaseFilter[] filters)
    {
        Operator = @operator;
        Filters = filters ?? throw new ArgumentNullException(nameof(filters));

        if (Filters.Count == 0)
            throw new ArgumentException("A logical filter must contain at least one child filter.", nameof(filters));

        if (@operator == FilterLogicalOperator.Not && Filters.Count != 1)
            throw new ArgumentException("A NOT filter must contain exactly one child filter.", nameof(filters));

        if (Filters.Any(x => x is null))
            throw new ArgumentException("A logical filter cannot contain null child filters.", nameof(filters));
    }

    public FilterLogicalOperator Operator { get; }
    public IReadOnlyList<BaseFilter> Filters { get; }
}

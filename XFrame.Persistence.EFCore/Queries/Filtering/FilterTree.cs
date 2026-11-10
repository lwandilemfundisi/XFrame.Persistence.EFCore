using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering;

/// <summary>Stores package-local nodes inside the public filter contract of XFrame.Persistence.</summary>
internal static class FilterTree
{
    private const string Marker = "__XFrame_EFCore_FilterTree__";
    internal sealed record Group(FilterLogicalOperator Operator, IReadOnlyList<BaseFilter> Children);
    internal sealed record Any(string Property, BaseFilter Predicate);

    internal static BaseFilter CreateGroup(FilterLogicalOperator op, params BaseFilter[] children)
        => new EqualityFilter(Marker, new Group(op, children));
    internal static BaseFilter CreateAny(string property, BaseFilter predicate)
        => new EqualityFilter(Marker, new Any(property, predicate));
    internal static bool TryGet(BaseFilter filter, out object node)
    {
        if (filter is EqualityFilter equality && equality.Property == Marker && equality.Value is Group or Any)
        { node = equality.Value; return true; }
        node = null!;
        return false;
    }
}

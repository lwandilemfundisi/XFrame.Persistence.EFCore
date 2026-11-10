using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering;

/// <summary>A predicate applied to elements of a collection navigation property.</summary>
public sealed class AnyFilter : BaseFilter
{
    public AnyFilter(string property, BaseFilter predicate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        ArgumentNullException.ThrowIfNull(predicate);
        Property = property;
        Predicate = predicate;
    }

    public string Property { get; }
    public BaseFilter Predicate { get; }
}

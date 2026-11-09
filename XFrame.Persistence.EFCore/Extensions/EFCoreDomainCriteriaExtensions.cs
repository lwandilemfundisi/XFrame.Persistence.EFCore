using XFrame.Common.Extensions;
using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.EFCore.Queries.Filtering.Builders;
using XFrame.Persistence.Extensions;
using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Extensions;

public static class EFCoreDomainCriteriaExtensions
{
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
            domainCriteria.SafeAnd(filter);

        return domainCriteria;
    }

    public static EFCoreDomainCriteria And(
        this EFCoreDomainCriteria domainCriteria,
        BaseFilter filter)
    {
        ArgumentNullException.ThrowIfNull(domainCriteria);
        ArgumentNullException.ThrowIfNull(filter);
        domainCriteria.SafeAnd(filter);
        return domainCriteria;
    }
}

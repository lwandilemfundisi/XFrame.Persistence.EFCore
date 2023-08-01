using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.Queries.CriteriaQueries;

namespace XFrame.Persistence.EFCore.Queries.CriteriaQueries
{
    public abstract class EFCoreCriteriaDomainQuery<TDomain> : CriteriaDomainQuery<TDomain, EFCoreDomainCriteria>
        where TDomain : class
    {
        public EFCoreCriteriaDomainQuery()
            : base()
        {
        }
    }
}

using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.Queries.CriteriaQueries;

namespace XFrame.Persistence.EFCore.Queries.CriteriaQueries
{
    public abstract class EFCoreCriteriaDomainQueryHandler<TDomain> : CriteriaDomainQueryHandler<TDomain, EFCoreDomainCriteria> 
        where TDomain : class
    {
        public EFCoreCriteriaDomainQueryHandler(IPersistenceFactory persistenceFactory)
            : base(persistenceFactory)
        {
        }
    }
}

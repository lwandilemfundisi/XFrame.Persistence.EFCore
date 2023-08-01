using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.Queries.NamedQueries;

namespace XFrame.Persistence.EFCore.Queries.NamedQueries
{
    public abstract class EFCoreNamedQueryHandler<TModel> 
        : StoredProcedureQueryHandler<TModel, EFCoreNamedCriteria> where TModel : class
    {
        public EFCoreNamedQueryHandler(IPersistenceFactory persistenceFactory)
            : base(persistenceFactory)
        {
        }
    }
}

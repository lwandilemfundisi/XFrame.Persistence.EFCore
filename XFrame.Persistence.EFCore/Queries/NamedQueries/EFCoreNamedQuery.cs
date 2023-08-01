using XFrame.Persistence.EFCore.Queries.Filtering;
using XFrame.Persistence.Queries.NamedQueries;

namespace XFrame.Persistence.EFCore.Queries.NamedQueries
{
    public abstract class EFCoreNamedQuery<TModel> 
        : StoredProcedureQuery<TModel, EFCoreNamedCriteria>
        where TModel : class
    {
    }
}

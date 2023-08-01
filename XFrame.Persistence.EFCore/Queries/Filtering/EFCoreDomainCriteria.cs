using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering
{
    public class EFCoreDomainCriteria : DomainCriteria
    {
        public Include Include { get; set; }
    }
}

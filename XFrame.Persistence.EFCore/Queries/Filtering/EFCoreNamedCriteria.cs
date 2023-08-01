using XFrame.Common.Extensions;
using XFrame.Persistence.Queries.Filterings;

namespace XFrame.Persistence.EFCore.Queries.Filtering
{
    public class EFCoreNamedCriteria : NamedCriteria
    {
        public override string ToString()
        {
            var result = "{0} ".FormatInvariantCulture(Name);

            foreach(var param in Parameters.Select((v, i) => new { i, v }))
            {
                if (param.i == 0)
                    result += $"@{param.v.Key}";
                else
                    result += $",@{param.v.Key}";
            }

            return result;
        }

    }
}

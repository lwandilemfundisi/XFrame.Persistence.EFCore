using XFrame.Common;
using XFrame.Common.Extensions;

namespace XFrame.Persistence.EFCore.Queries.Filtering
{
    public class Include
    {
        #region Constructors

        public Include(string propertyName)
        {
            PropertyName = propertyName;
        }

        #endregion

        #region Properties

        public string PropertyName { get; set; }

        #endregion

        #region Virtual Methods

        public override string ToString()
        {
            return "Framework.Persistence.Include(PropertyName:{0})".FormatInvariantCulture(PropertyName);
        }

        #endregion
    }
}

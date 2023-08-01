using Microsoft.EntityFrameworkCore;

namespace XFrame.Persistence.EFCore
{
    public interface IDbContextProvider<out TDbContext> where TDbContext : DbContext
    {
        TDbContext CreateContext();
    }
}
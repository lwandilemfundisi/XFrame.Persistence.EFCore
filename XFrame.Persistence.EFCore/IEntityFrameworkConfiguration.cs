using Microsoft.Extensions.DependencyInjection;

namespace XFrame.Persistence.EFCore
{
    public interface IEntityFrameworkConfiguration
    {
        void Apply(IServiceCollection serviceCollection);
    }
}
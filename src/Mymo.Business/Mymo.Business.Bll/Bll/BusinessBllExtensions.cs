using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Business.Bll;

public static class BusinessBllExtensions
{
    public static IServiceCollection AddBusinessBll(this IServiceCollection services)
    {
        services.AddScoped<BusinessService>();
        return services;
    }
}

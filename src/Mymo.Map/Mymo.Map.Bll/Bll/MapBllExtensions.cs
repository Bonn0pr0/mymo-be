using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Map.Bll;

public static class MapBllExtensions
{
    public static IServiceCollection AddMapBll(this IServiceCollection services)
    {
        services.AddScoped<MapService>();
        return services;
    }
}

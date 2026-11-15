using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mymo.Map.Data;

namespace Mymo.Map.Dal;

public static class MapDalExtensions
{
    public static IServiceCollection AddMapDal(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<MapDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("Database")));
        services.AddScoped<IMapRepository, MapRepository>();
        return services;
    }
}

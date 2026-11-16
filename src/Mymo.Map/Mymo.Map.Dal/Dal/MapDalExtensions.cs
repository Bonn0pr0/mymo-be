using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mymo.Map.Data;

namespace Mymo.Map.Dal;

public static class MapDalExtensions
{
    public static IServiceCollection AddMapDal(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Database") ?? "";
        services.AddDbContext<MapDbContext>(options =>
        {
            if (IsSqlServer(connectionString))
            {
                options.UseSqlServer(connectionString, sql =>
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });
        services.AddScoped<IMapRepository, MapRepository>();
        return services;
    }

    private static bool IsSqlServer(string connStr) =>
        !string.IsNullOrWhiteSpace(connStr) &&
        (connStr.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
         connStr.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
         connStr.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
         connStr.Contains("1433") ||
         connStr.Contains("sqlserver", StringComparison.OrdinalIgnoreCase) ||
         connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase));
}

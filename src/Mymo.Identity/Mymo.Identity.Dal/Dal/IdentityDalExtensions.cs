using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mymo.Identity.Data;

namespace Mymo.Identity.Dal;

public static class IdentityDalExtensions
{
    public static IServiceCollection AddIdentityDal(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Database") ?? "";
        services.AddDbContext<IdentityDbContext>(options =>
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
        services.AddScoped<IIdentityRepository, IdentityRepository>();
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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mymo.Identity.Data;

namespace Mymo.Identity.Dal;

public static class IdentityDalExtensions
{
    public static IServiceCollection AddIdentityDal(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("Database")));
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        return services;
    }
}

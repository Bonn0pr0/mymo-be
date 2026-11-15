using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mymo.Business.Data;

namespace Mymo.Business.Dal;

public static class BusinessDalExtensions
{
    public static IServiceCollection AddBusinessDal(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<BusinessDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("Database")));
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mymo.Social.Data;

namespace Mymo.Social.Dal;

public static class SocialDalExtensions
{
    public static IServiceCollection AddSocialDal(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<SocialDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("Database")));
        services.AddScoped<ISocialRepository, SocialRepository>();
        return services;
    }
}

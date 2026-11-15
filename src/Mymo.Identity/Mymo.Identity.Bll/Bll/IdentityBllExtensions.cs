using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Identity.Bll;

public static class IdentityBllExtensions
{
    public static IServiceCollection AddIdentityBll(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<AdminService>();
        return services;
    }
}

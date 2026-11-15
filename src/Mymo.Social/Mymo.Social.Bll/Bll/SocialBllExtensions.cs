using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Social.Bll;

public static class SocialBllExtensions
{
    public static IServiceCollection AddSocialBll(this IServiceCollection services)
    {
        services.AddScoped<PostService>();
        services.AddScoped<FriendService>();
        services.AddScoped<NotificationService>();
        return services;
    }
}

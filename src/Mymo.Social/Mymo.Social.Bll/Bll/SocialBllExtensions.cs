using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Social.Bll;

public static class SocialBllExtensions
{
    public static IServiceCollection AddSocialBll(this IServiceCollection services)
    {
        services.AddScoped<PostService>();
        services.AddScoped<FriendService>();
        services.AddScoped<NotificationService>();
        services.AddHttpClient<HanaService>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
        .RedactLoggedHeaders(["x-goog-api-key"]);
        return services;
    }
}

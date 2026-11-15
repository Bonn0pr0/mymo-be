using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Chat.Bll;

public static class ChatBllExtensions
{
    public static IServiceCollection AddChatBll(this IServiceCollection services)
    {
        services.AddScoped<ChatService>();
        return services;
    }
}

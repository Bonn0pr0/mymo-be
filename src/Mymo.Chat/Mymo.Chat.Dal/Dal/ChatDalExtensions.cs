using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mymo.Chat.Data;

namespace Mymo.Chat.Dal;

public static class ChatDalExtensions
{
    public static IServiceCollection AddChatDal(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ChatDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("Database")));
        services.AddScoped<IChatRepository, ChatRepository>();
        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Shared.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMymoMessaging(this IServiceCollection services, IConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config["Messaging:RabbitMq"]))
            services.AddSingleton<ICommandBus, RabbitCommandBus>();

        if (!string.IsNullOrWhiteSpace(config["Messaging:Kafka"]))
            services.AddSingleton<IEventBus, KafkaEventBus>();

        return services;
    }

    public static IServiceCollection AddRabbitConsumer<T>(
        this IServiceCollection services,
        string queue,
        Func<IServiceProvider, T, CancellationToken, Task> handler)
    {
        services.AddHostedService(sp =>
            new RabbitConsumerHostedService<T>(
                sp.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>(),
                queue,
                handler));
        return services;
    }

    public static IServiceCollection AddKafkaConsumer<T>(
        this IServiceCollection services,
        string topic,
        string groupId,
        Func<IServiceProvider, T, CancellationToken, Task> handler)
    {
        services.AddHostedService(sp =>
            new KafkaConsumerHostedService<T>(
                sp.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>(),
                topic,
                groupId,
                handler));
        return services;
    }
}

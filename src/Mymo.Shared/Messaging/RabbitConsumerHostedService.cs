using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Mymo.Shared.Messaging;

public sealed class RabbitConsumerHostedService<T> : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger _log;
    private readonly string _queue;
    private readonly Func<IServiceProvider, T, CancellationToken, Task> _handler;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitConsumerHostedService(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILoggerFactory logs,
        string queue,
        Func<IServiceProvider, T, CancellationToken, Task> handler)
    {
        _scopes = scopes;
        _config = config;
        _log = logs.CreateLogger($"RabbitConsumer<{typeof(T).Name}>");
        _queue = queue;
        _handler = handler;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_config["Messaging:RabbitMq"] ?? "amqp://guest:guest@localhost:5672"),
            DispatchConsumersAsync = true,
        };
        _connection = factory.CreateConnection($"mymo-consumer-{_queue}");
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
        _channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(args.Body.ToArray());
                var message = JsonSerializer.Deserialize<T>(json, Internal.JsonOptions);
                if (message == null)
                {
                    _channel.BasicNack(args.DeliveryTag, false, false);
                    return;
                }

                using var scope = _scopes.CreateScope();
                await _handler(scope.ServiceProvider, message, stoppingToken);
                _channel.BasicAck(args.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed handling {Queue}", _queue);
                _channel?.BasicNack(args.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(_queue, autoAck: false, consumer);
        _log.LogInformation("Consuming queue {Queue}", _queue);
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        try { _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Dispose(); } catch { /* ignore */ }
        base.Dispose();
    }
}

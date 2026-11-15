using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Mymo.Shared.Messaging;

public sealed class RabbitCommandBus : ICommandBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitCommandBus> _log;

    public RabbitCommandBus(IConfiguration config, ILogger<RabbitCommandBus> log)
    {
        _log = log;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(config["Messaging:RabbitMq"] ?? "amqp://guest:guest@localhost:5672"),
            DispatchConsumersAsync = true,
        };
        _connection = factory.CreateConnection("mymo-commands");
        _channel = _connection.CreateModel();
    }

    public Task PublishAsync<T>(string queue, T message, CancellationToken ct = default)
    {
        _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, Internal.JsonOptions));
        var props = _channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2;
        _channel.BasicPublish("", queue, props, body);
        _log.LogInformation("Published command {Queue}", queue);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _channel.Dispose(); } catch { /* ignore */ }
        try { _connection.Dispose(); } catch { /* ignore */ }
    }
}

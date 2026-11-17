using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Mymo.Shared.Messaging;

public sealed class RabbitCommandBus : ICommandBus, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ILogger<RabbitCommandBus> _log;
    private readonly IConfiguration _config;
    private DateTime _unavailableUntil = DateTime.MinValue;

    public RabbitCommandBus(IConfiguration config, ILogger<RabbitCommandBus> log)
    {
        _log = log;
        _config = config;
    }

    private void EnsureChannel()
    {
        if (_channel != null && _channel.IsOpen) return;
        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_config["Messaging:RabbitMq"] ?? "amqp://guest:guest@localhost:5672"),
                DispatchConsumersAsync = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(2),
            };
            _connection = factory.CreateConnection("mymo-commands");
            _channel = _connection.CreateModel();
        }
        catch (Exception ex)
        {
            _unavailableUntil = DateTime.UtcNow.AddSeconds(30);
            _log.LogWarning("RabbitMQ connection failed: {Message}", ex.Message);
        }
    }

    public Task PublishAsync<T>(string queue, T message, CancellationToken ct = default)
    {
        if (DateTime.UtcNow < _unavailableUntil)
        {
            return Task.CompletedTask;
        }

        try
        {
            EnsureChannel();
            if (_channel == null || !_channel.IsOpen)
            {
                _log.LogWarning("RabbitMQ unavailable. Command {Queue} skipped.", queue);
                return Task.CompletedTask;
            }
            _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, Internal.JsonOptions));
            var props = _channel.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2;
            _channel.BasicPublish("", queue, props, body);
            _log.LogInformation("Published command {Queue}", queue);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed publishing command {Queue}", queue);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Dispose(); } catch { /* ignore */ }
    }
}

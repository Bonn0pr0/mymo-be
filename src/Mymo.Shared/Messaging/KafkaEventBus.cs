using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mymo.Shared.Messaging;

public sealed class KafkaEventBus : IEventBus, IDisposable
{
    private IProducer<Null, string>? _producer;
    private readonly ILogger<KafkaEventBus> _log;
    private readonly IConfiguration _config;

    public KafkaEventBus(IConfiguration config, ILogger<KafkaEventBus> log)
    {
        _log = log;
        _config = config;
    }

    private void EnsureProducer()
    {
        if (_producer != null) return;
        try
        {
            var bootstrap = _config["Messaging:Kafka"] ?? "localhost:9092";
            _producer = new ProducerBuilder<Null, string>(new ProducerConfig
            {
                BootstrapServers = bootstrap,
                Acks = Acks.Leader,
            }).Build();
        }
        catch (Exception ex)
        {
            _log.LogWarning("Kafka producer initialization failed: {Message}", ex.Message);
        }
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken ct = default)
    {
        try
        {
            EnsureProducer();
            if (_producer == null)
            {
                _log.LogWarning("Kafka unavailable. Event {Topic} skipped.", topic);
                return;
            }
            var payload = JsonSerializer.Serialize(message, Internal.JsonOptions);
            await _producer.ProduceAsync(topic, new Message<Null, string> { Value = payload }, ct);
            _log.LogInformation("Published event {Topic}", topic);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed publishing event {Topic}", topic);
        }
    }

    public void Dispose()
    {
        try { _producer?.Dispose(); } catch { /* ignore */ }
    }
}

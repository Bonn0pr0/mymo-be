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
    private DateTime _unavailableUntil = DateTime.MinValue;

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
                MessageTimeoutMs = 2000,
                SocketTimeoutMs = 2000,
            }).Build();
        }
        catch (Exception ex)
        {
            _unavailableUntil = DateTime.UtcNow.AddSeconds(30);
            _log.LogWarning("Kafka producer initialization failed: {Message}", ex.Message);
        }
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken ct = default)
    {
        if (DateTime.UtcNow < _unavailableUntil)
        {
            return;
        }

        try
        {
            EnsureProducer();
            if (_producer == null)
            {
                _unavailableUntil = DateTime.UtcNow.AddSeconds(30);
                _log.LogWarning("Kafka unavailable. Event {Topic} skipped.", topic);
                return;
            }
            var payload = JsonSerializer.Serialize(message, Internal.JsonOptions);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
            await _producer.ProduceAsync(topic, new Message<Null, string> { Value = payload }, timeoutCts.Token);
            _log.LogInformation("Published event {Topic}", topic);
        }
        catch (Exception ex)
        {
            _unavailableUntil = DateTime.UtcNow.AddSeconds(30);
            _log.LogWarning("Kafka publish failed or timed out for {Topic}: {Message}. Skipping Kafka for 30s.", topic, ex.Message);
        }
    }

    public void Dispose()
    {
        try { _producer?.Dispose(); } catch { /* ignore */ }
    }
}

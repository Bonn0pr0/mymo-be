using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mymo.Shared.Messaging;

public sealed class KafkaEventBus : IEventBus, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaEventBus> _log;

    public KafkaEventBus(IConfiguration config, ILogger<KafkaEventBus> log)
    {
        _log = log;
        var bootstrap = config["Messaging:Kafka"] ?? "localhost:9092";
        _producer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = bootstrap,
            Acks = Acks.Leader,
        }).Build();
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(message, Internal.JsonOptions);
        await _producer.ProduceAsync(topic, new Message<Null, string> { Value = payload }, ct);
        _log.LogInformation("Published event {Topic}", topic);
    }

    public void Dispose() => _producer.Dispose();
}

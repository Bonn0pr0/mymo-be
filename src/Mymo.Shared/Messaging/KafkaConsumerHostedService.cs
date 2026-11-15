using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mymo.Shared.Messaging;

public sealed class KafkaConsumerHostedService<T> : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger _log;
    private readonly string _topic;
    private readonly string _groupId;
    private readonly Func<IServiceProvider, T, CancellationToken, Task> _handler;

    public KafkaConsumerHostedService(
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILoggerFactory logs,
        string topic,
        string groupId,
        Func<IServiceProvider, T, CancellationToken, Task> handler)
    {
        _scopes = scopes;
        _config = config;
        _log = logs.CreateLogger($"KafkaConsumer<{typeof(T).Name}>");
        _topic = topic;
        _groupId = groupId;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = _config["Messaging:Kafka"] ?? "localhost:9092";
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();

        consumer.Subscribe(_topic);
        _log.LogInformation("Consuming topic {Topic} as {Group}", _topic, _groupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value == null) continue;
                var message = JsonSerializer.Deserialize<T>(result.Message.Value, Internal.JsonOptions);
                if (message == null) continue;

                using var scope = _scopes.CreateScope();
                await _handler(scope.ServiceProvider, message, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _log.LogError(ex, "Kafka consume error on {Topic}", _topic);
                await Task.Delay(1000, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed handling Kafka {Topic}", _topic);
                await Task.Delay(1000, stoppingToken);
            }
        }

        consumer.Close();
    }
}

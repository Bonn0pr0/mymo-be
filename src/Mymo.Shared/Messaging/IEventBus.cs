namespace Mymo.Shared.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(string topic, T message, CancellationToken ct = default);
}

namespace Mymo.Shared.Messaging;

public interface ICommandBus
{
    Task PublishAsync<T>(string queue, T message, CancellationToken ct = default);
}

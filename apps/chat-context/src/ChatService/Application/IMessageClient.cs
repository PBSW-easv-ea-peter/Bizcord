namespace ChatService.Application;

/// <summary>
/// Abstraktion over message brokeren (samme mønster som i Real-Time Communication Service).
/// Kun Publish - ChatService abonnerer ikke på noget endnu.
/// </summary>
public interface IMessageClient
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
}

namespace ChatService.Application;

/// <summary>
/// Abstraction over the message broker (same pattern as in Real-Time Communication Service).
/// Used for publishing and for the single subscription (rtc.message-delivered) in Infrastructure/Messaging/MessageDeliveredConsumer.
/// </summary>
public interface IMessageClient
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
    Task SubscribeAsync<T>(string subscriptionId, Func<T, Task> handler);
}

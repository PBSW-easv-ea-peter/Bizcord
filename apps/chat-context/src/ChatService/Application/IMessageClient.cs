namespace ChatService.Application;

/// <summary>
/// Abstraction over the message broker (same pattern as in Real-Time Communication Service).
/// Publish only - the single subscription (rtc.message-delivered) lives in Infrastructure/Messaging/MessageDeliveredConsumer.
/// </summary>
public interface IMessageClient
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
}

namespace ChatService.Application;

/// <summary>
/// Abstraktion over message brokeren (samme mønster som i Real-Time Communication Service).
/// Kun Publish - det ene abonnement (rtc.message-delivered) ligger i Infrastructure/Messaging/MessageDeliveredConsumer.
/// </summary>
public interface IMessageClient
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
    Task SubscribeAsync<T>(string subscriptionId, Func<T, Task> handler);
}

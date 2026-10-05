namespace RealTimeCommunicationServer.Messaging;

/// <summary>
/// Handles one message type. Implementations are discovered and registered automatically by AddMessageHandlers -
/// a new message type only requires a new class.
/// </summary>
public interface IMessageHandler<in T>
{
    Task HandleAsync(
        T message,
        CancellationToken cancellationToken);
}

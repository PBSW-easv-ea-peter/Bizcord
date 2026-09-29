namespace RealTimeCommunicationServer.Messaging;

/// <summary>
/// Håndterer én beskedtype. Implementeringer findes og registreres automatisk af AddMessageHandlers -
/// en ny beskedtype kræver kun en ny klasse.
/// </summary>
public interface IMessageHandler<in T>
{
    Task HandleAsync(
        T message,
        CancellationToken cancellationToken);
}

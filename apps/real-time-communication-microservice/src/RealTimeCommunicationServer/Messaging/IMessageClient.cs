namespace RealTimeCommunicationServer.Messaging;

public interface IMessageClient
{
    Task Publish<T>(
        T message,
        CancellationToken cancellationToken = default);
    
    Task Subscribe<T>(
        string subscriptionId,
        Func<T, Task> handler);
}
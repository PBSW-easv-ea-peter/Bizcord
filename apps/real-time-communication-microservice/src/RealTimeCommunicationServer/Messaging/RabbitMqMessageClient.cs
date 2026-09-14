using EasyNetQ;

namespace RealTimeCommunicationServer.Messaging;

public class RabbitMqMessageClient : IMessageClient
{
    private readonly IBus _bus;

    public RabbitMqMessageClient(IBus bus)
    {
        _bus = bus;
    }

    public Task Publish<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        return _bus.PubSub.PublishAsync(
            message,
            cancellationToken);
    }

    public Task Subscribe<T>(
        string subscriptionId,
        Func<T, Task> handler)
    {
        return _bus.PubSub.SubscribeAsync(
            subscriptionId,
            handler);
    }
}
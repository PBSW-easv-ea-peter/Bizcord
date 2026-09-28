using ChatService.Application;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

public sealed class RabbitMqMessageClient(IBus bus) : IMessageClient
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) =>
        bus.PubSub.PublishAsync(message, cancellationToken);
}

using System.Diagnostics;
using ChatService.Application;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

public sealed class RabbitMqMessageClient(IBus bus) : IMessageClient
{
    /// <summary>W3C Trace Context-header - consumeren fortsætter samme trace. Se docs/contracts.md.</summary>
    public const string TraceParentHeader = "traceparent";

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) =>
        bus.PubSub.PublishAsync(message, configuration =>
        {
            if (Activity.Current?.Id is { } traceParent)
                configuration.WithHeaders(new Dictionary<string, object> { [TraceParentHeader] = traceParent });
        }, cancellationToken);
}

using System.Diagnostics;
using System.Text;
using ChatService.Application;
using EasyNetQ;
using EasyNetQ.Topology;

namespace ChatService.Infrastructure.Messaging;

public sealed class RabbitMqMessageClient : IMessageClient
{
    /// <summary>W3C Trace Context header - the consumer continues the same trace. See docs/contracts.md.</summary>
    public const string TraceParentHeader = "traceparent";

    private readonly IBus _bus;
    private readonly IConventions _conventions;

    public RabbitMqMessageClient(IBus bus, IConventions conventions)
    {
        _bus = bus;
        _conventions = conventions;
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) =>
        _bus.PubSub.PublishAsync(message, configuration =>
        {
            if (Activity.Current?.Id is { } traceParent)
                configuration.WithHeaders(new Dictionary<string, object> { [TraceParentHeader] = traceParent });
        }, cancellationToken);

    /// <summary>
    /// Same topology as PubSub.SubscribeAsync (EasyNetQ's naming conventions), but via the advanced API,
    /// because PubSub does not give access to the message headers - and thereby traceparent.
    /// </summary>
    public async Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler)
    {
        var advanced = _bus.Advanced;
        var exchangeName = _conventions.ExchangeNamingConvention(typeof(T));

        var exchange = await advanced.ExchangeDeclareAsync(
            exchangeName,
            configuration => configuration.WithType(_conventions.ExchangeTypeConvention(typeof(T))));
        var queue = await advanced.QueueDeclareAsync(
            _conventions.QueueNamingConvention(typeof(T), subscriptionId));
        await advanced.BindAsync(exchange, queue, "#");

        await advanced.ConsumeAsync<T>(
            queue,
            async (message, _) =>
            {
                // Continues the publisher's trace. Without the header it becomes a new root trace.
                using var activity = new Activity($"{exchangeName} process");
                if (ReadTraceParent(message.Properties) is { } traceParent)
                    activity.SetParentId(traceParent);
                activity.Start();

                await handler(message.Body);
            });
    }

    private static string? ReadTraceParent(
        MessageProperties properties)
    {
        // RabbitMQ delivers string headers as byte[].
        if (properties.Headers is null || !properties.Headers.TryGetValue(TraceParentHeader, out var value))
            return null;

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => null
        };
    }
}

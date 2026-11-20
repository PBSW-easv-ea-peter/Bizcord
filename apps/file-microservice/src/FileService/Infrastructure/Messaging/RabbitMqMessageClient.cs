using System.Diagnostics;
using System.Text;
using EasyNetQ;
using FileService.Application;

namespace FileService.Infrastructure.Messaging;

/// <summary>
/// Same as ChatService's RabbitMqMessageClient (copied, not shared - no shared code between services):
/// publishes with the traceparent header and continues the publisher's trace when consuming.
/// </summary>
public sealed class RabbitMqMessageClient(IBus bus, IConventions conventions) : IMessageClient
{
    public const string TraceParentHeader = "traceparent";

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) =>
        bus.PubSub.PublishAsync(message, configuration =>
        {
            if (Activity.Current?.Id is { } traceParent)
                configuration.WithHeaders(new Dictionary<string, object> { [TraceParentHeader] = traceParent });
        }, cancellationToken);

    public async Task SubscribeAsync<T>(string subscriptionId, Func<T, Task> handler)
    {
        var advanced = bus.Advanced;
        var exchangeName = conventions.ExchangeNamingConvention(typeof(T));

        var exchange = await advanced.ExchangeDeclareAsync(
            exchangeName,
            configuration => configuration.WithType(conventions.ExchangeTypeConvention(typeof(T))));
        var queue = await advanced.QueueDeclareAsync(conventions.QueueNamingConvention(typeof(T), subscriptionId));
        await advanced.BindAsync(exchange, queue, "#");

        await advanced.ConsumeAsync<T>(
            queue,
            async (message, _) =>
            {
                using var activity = new Activity($"{exchangeName} process");
                if (ReadTraceParent(message.Properties) is { } traceParent)
                    activity.SetParentId(traceParent);
                activity.Start();

                await handler(message.Body);
            });
    }

    private static string? ReadTraceParent(MessageProperties properties)
    {
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

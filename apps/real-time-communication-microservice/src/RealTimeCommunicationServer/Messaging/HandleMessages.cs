using System.Reflection;

namespace RealTimeCommunicationServer.Messaging;

/// <summary>
/// Subscribes to every message type that has a handler (see MessageHandlerRegistry) and forwards the message
/// to all handlers for that type. Knows no concrete message types.
/// Subscribes in StartAsync so the host only reports ready once the queues are bound - otherwise events
/// arriving in the meantime can be lost.
/// </summary>
public class HandleMessages : IHostedService, IDisposable
{
    // Gives handlers a token that is cancelled on shutdown (StartAsync's token only covers startup).
    private readonly CancellationTokenSource _stopping = new();

    private const string SubscriptionId = "real-time-server";

    private static readonly MethodInfo SubscribeMethod =
        typeof(HandleMessages).GetMethod(nameof(SubscribeAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly IMessageClient _messageClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessageHandlerRegistry _registry;

    public HandleMessages(
        IMessageClient messageClient,
        IServiceScopeFactory scopeFactory,
        MessageHandlerRegistry registry)
    {
        _messageClient = messageClient;
        _scopeFactory = scopeFactory;
        _registry = registry;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        // The type is only known at runtime, so the generic method is closed via reflection - the only place.
        foreach (var messageType in _registry.MessageTypes)
            await (Task)SubscribeMethod.MakeGenericMethod(messageType).Invoke(this, [_stopping.Token])!;
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose() => _stopping.Dispose();

    // One subscription per type, not per handler: two subscriptions with the same id share a queue
    // and would compete for the messages instead of each getting its own copy.
    private Task SubscribeAsync<T>(
        CancellationToken stoppingToken)
    {
        return _messageClient.Subscribe<T>(
            SubscriptionId,
            async message =>
            {
                // One scope per message, like one per HTTP request - so handlers can have scoped dependencies.
                await using var scope = _scopeFactory.CreateAsyncScope();

                foreach (var handler in scope.ServiceProvider.GetServices<IMessageHandler<T>>())
                    await handler.HandleAsync(message, stoppingToken);
            });
    }
}

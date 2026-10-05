using System.Reflection;

namespace RealTimeCommunicationServer.Messaging;

/// <summary>
/// Abonnerer på hver beskedtype, der har en handler (se MessageHandlerRegistry), og sender beskeden videre
/// til alle handlers for typen. Kender ingen konkrete beskedtyper.
/// Abonnerer i StartAsync, så hosten først melder sig klar, når køerne er bundet - ellers kan events,
/// der kommer i mellemtiden, gå tabt.
/// </summary>
public class HandleMessages : IHostedService, IDisposable
{
    // Giver handlers et token, der annulleres ved shutdown (StartAsync's token gælder kun opstarten).
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
        // Typen kendes først ved runtime, så den generiske metode lukkes med reflection - det eneste sted.
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

    // Én subscription pr. type, ikke pr. handler: to subscriptions med samme id deler kø
    // og ville konkurrere om beskederne i stedet for at få hver sin kopi.
    private Task SubscribeAsync<T>(
        CancellationToken stoppingToken)
    {
        return _messageClient.Subscribe<T>(
            SubscriptionId,
            async message =>
            {
                // Ét scope pr. besked, ligesom ét pr. HTTP-request - så handlers kan have scoped afhængigheder.
                await using var scope = _scopeFactory.CreateAsyncScope();

                foreach (var handler in scope.ServiceProvider.GetServices<IMessageHandler<T>>())
                    await handler.HandleAsync(message, stoppingToken);
            });
    }
}

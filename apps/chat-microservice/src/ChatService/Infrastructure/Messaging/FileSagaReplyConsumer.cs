using ChatService.Application;
using ChatService.Contracts;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Subscribes to FileService's replies in the file-message saga and hands each one to its IMessageHandler&lt;T&gt;
/// (the orchestrator), one DI scope per message (same pattern as MessageDeliveredConsumer).
/// </summary>
public sealed class FileSagaReplyConsumer(IMessageClient messageClient, IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Subscribe<QuotaReserved>(cancellationToken);
        await Subscribe<QuotaRejected>(cancellationToken);
        await Subscribe<QuotaReleased>(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Each message type gets its own queue (EasyNetQ names it from the type and the subscription id).
    private Task Subscribe<T>(CancellationToken cancellationToken) =>
        messageClient.SubscribeAsync<T>(
            MessageDeliveredConsumer.SubscriptionId,
            async message =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>().HandleAsync(message, cancellationToken);
            });
}

using ChatService.Application;
using ChatService.Contracts;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Subscribes to FileService's replies in the file-message saga and hands them to the orchestrator,
/// one DI scope per message (same pattern as MessageDeliveredConsumer).
/// </summary>
public sealed class FileSagaReplyConsumer(IMessageClient messageClient, IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Subscribe<QuotaReserved>((orchestrator, message) => orchestrator.HandleAsync(message, cancellationToken));
        await Subscribe<QuotaRejected>((orchestrator, message) => orchestrator.HandleAsync(message, cancellationToken));
        await Subscribe<QuotaReleased>((orchestrator, message) => orchestrator.HandleAsync(message, cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Each message type gets its own queue (EasyNetQ names it from the type and the subscription id).
    private Task Subscribe<T>(Func<FileMessageSagaOrchestrator, T, Task> handle) =>
        messageClient.SubscribeAsync<T>(
            MessageDeliveredConsumer.SubscriptionId,
            async message =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await handle(scope.ServiceProvider.GetRequiredService<FileMessageSagaOrchestrator>(), message);
            });
}

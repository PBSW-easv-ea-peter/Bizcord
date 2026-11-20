using FileService.Application;
using FileService.Contracts;

namespace FileService.Infrastructure.Messaging;

/// <summary>Subscribes to the saga's commands in StartAsync (the host is ready once the queues are bound), one DI scope per message.</summary>
public sealed class QuotaCommandConsumer(IMessageClient messageClient, IServiceScopeFactory scopeFactory) : IHostedService
{
    public const string SubscriptionId = "file-service";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Subscribe<ReserveQuota>((participant, command) => participant.HandleAsync(command, cancellationToken));
        await Subscribe<ReleaseQuota>((participant, command) => participant.HandleAsync(command, cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task Subscribe<T>(Func<QuotaSagaParticipant, T, Task> handle) =>
        messageClient.SubscribeAsync<T>(
            SubscriptionId,
            async command =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await handle(scope.ServiceProvider.GetRequiredService<QuotaSagaParticipant>(), command);
            });
}

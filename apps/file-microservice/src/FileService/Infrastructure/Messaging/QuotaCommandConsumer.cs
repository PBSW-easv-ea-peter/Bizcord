using FileService.Application;
using FileService.Contracts;

namespace FileService.Infrastructure.Messaging;

/// <summary>
/// Subscribes to the saga's commands in StartAsync (the host is ready once the queues are bound) and hands each one
/// to its IMessageHandler&lt;T&gt; (the participant), one DI scope per message.
/// </summary>
public sealed class QuotaCommandConsumer(IMessageClient messageClient, IServiceScopeFactory scopeFactory) : IHostedService
{
    public const string SubscriptionId = "file-service";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Subscribe<ReserveQuota>(cancellationToken);
        await Subscribe<ReleaseQuota>(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task Subscribe<T>(CancellationToken cancellationToken) =>
        messageClient.SubscribeAsync<T>(
            SubscriptionId,
            async command =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>().HandleAsync(command, cancellationToken);
            });
}

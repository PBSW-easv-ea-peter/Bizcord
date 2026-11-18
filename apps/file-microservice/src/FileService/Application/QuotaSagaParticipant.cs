using Bizcord.Logging;
using FileService.Contracts;

namespace FileService.Application;

/// <summary>
/// FileService's part of ChatService's file-message saga. A participant, not a coordinator:
/// it does what the command says, answers with an event, and knows nothing about the next step.
/// </summary>
public sealed class QuotaSagaParticipant(
    IFileRepository files,
    IMessageClient messageClient,
    QuotaSettings settings,
    ILogger<QuotaSagaParticipant> logger) :
    IMessageHandler<ReserveQuota>,
    IMessageHandler<ReleaseQuota>
{
    public async Task HandleAsync(ReserveQuota command, CancellationToken cancellationToken = default)
    {
        var result = await files.ReserveQuotaAsync(command.SagaId, command.FileId, command.UserId, settings.StandardLimitBytes, cancellationToken);

        switch (result.Outcome)
        {
            case ReservationOutcome.Reserved:
            case ReservationOutcome.AlreadyReserved:
                var file = result.File!;
                logger.Information("Quota reserved.", new { command.SagaId, command.FileId, file.SizeBytes, Outcome = result.Outcome.ToString() });
                await messageClient.TryPublishAsync(new QuotaReserved(
                    command.SagaId, file.Id, command.UserId, file.FileName, file.ContentType, file.SizeBytes), logger);
                break;

            case ReservationOutcome.QuotaExceeded:
                var quota = result.Quota!;
                var reason = $"Storage quota exceeded: the file is {result.File!.SizeBytes} bytes, " +
                             $"but only {quota.RemainingBytes} of {quota.LimitBytes} bytes are left ({quota.Tier}). Upgrade to Premium for more storage.";
                logger.Information("Quota rejected - temporary file deleted.", new { command.SagaId, command.FileId, result.File.SizeBytes, quota.RemainingBytes });
                await messageClient.TryPublishAsync(new QuotaRejected(command.SagaId, command.FileId, command.UserId, reason), logger);
                break;

            case ReservationOutcome.NotAvailable:
                logger.Warning("Quota rejected - file not available.", new { command.SagaId, command.FileId });
                await messageClient.TryPublishAsync(new QuotaRejected(
                    command.SagaId, command.FileId, command.UserId, "The file does not exist, belongs to another user or is already attached."), logger);
                break;
        }
    }

    /// <summary>Always answers QuotaReleased - also when there was nothing to release, so the orchestrator can finish.</summary>
    public async Task HandleAsync(ReleaseQuota command, CancellationToken cancellationToken = default)
    {
        var released = await files.ReleaseAsync(command.SagaId, command.FileId, command.UserId, cancellationToken);

        logger.Information(released ? "Quota released and file deleted." : "Nothing to release.", new { command.SagaId, command.FileId });

        await messageClient.TryPublishAsync(new QuotaReleased(command.SagaId, command.FileId, command.UserId), logger);
    }
}

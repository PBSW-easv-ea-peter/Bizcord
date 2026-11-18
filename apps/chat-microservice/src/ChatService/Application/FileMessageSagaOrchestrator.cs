using Bizcord.Logging;
using ChatService.Contracts;
using ChatService.Domain;

namespace ChatService.Application;

/// <summary>
/// Orchestrator for the saga "send a message with a file" (docs/documentation/saga-file-attachment.md).
/// Receives every reply in the saga and decides what happens next - the whole flow is readable here.
///
///   1. FileService: file uploaded and stored temporarily (POST /files - before the saga starts)
///   2. FileService: reserve quota          ReserveQuota  → QuotaReserved | QuotaRejected
///   3. ChatService: save the message       (local)       → chat.message-sent
///   Compensation for 2 (and 1):            ReleaseQuota  → QuotaReleased
///
/// The message is saved only after the quota is reserved, so nobody has seen a message that is later rolled back.
/// </summary>
public sealed class FileMessageSagaOrchestrator(
    IChatRepository chats,
    IFileMessageSagaRepository sagas,
    IMessageClient messageClient,
    TimeProvider time,
    ILogger<FileMessageSagaOrchestrator> logger)
{
    public async Task<FileMessageSaga> StartAsync(Guid chatId, Guid senderUserId, Guid fileId, string? content, CancellationToken cancellationToken = default)
    {
        var chat = await chats.GetAsync(chatId, cancellationToken)
            ?? throw new NotFoundException($"Chat '{chatId}' was not found.");

        var messageContent = string.IsNullOrWhiteSpace(content) ? null : new MessageContent(content);
        var saga = FileMessageSaga.Start(chat, senderUserId, fileId, messageContent, time.GetUtcNowInMicroseconds());
        await sagas.AddAsync(saga, cancellationToken);

        logger.Information("File message saga started.", new { saga.SagaId, saga.ChatId, saga.FileId });

        // If this is lost (at-most-once), nobody answers and the timeout job compensates.
        await messageClient.TryPublishAsync(new ReserveQuota(saga.SagaId, saga.FileId, saga.SenderUserId), logger);

        return saga;
    }

    /// <summary>Only the sender can follow their saga. Anyone else gets 404 - the saga is not theirs to know about.</summary>
    public async Task<FileMessageSaga> GetAsync(Guid chatId, Guid sagaId, Guid requestedByUserId, CancellationToken cancellationToken = default)
    {
        var saga = await sagas.GetAsync(sagaId, cancellationToken);

        return saga is not null && saga.ChatId == chatId && saga.SenderUserId == requestedByUserId
            ? saga
            : throw new NotFoundException($"File message '{sagaId}' was not found in chat '{chatId}'.");
    }

    /// <summary>Step 2 succeeded → step 3: save the message. If that is not allowed any more, compensate step 2.</summary>
    public async Task HandleAsync(QuotaReserved reply, CancellationToken cancellationToken = default)
    {
        var saga = await sagas.GetAsync(reply.SagaId, cancellationToken);
        if (saga is null)
        {
            logger.Warning("QuotaReserved for an unknown saga - ignored.", new { reply.SagaId, reply.FileId });
            return;
        }

        if (saga.Status != FileMessageSagaStatus.AwaitingQuota)
        {
            await HandleLateReservationAsync(saga);
            return;
        }

        var now = time.GetUtcNowInMicroseconds();
        var chat = await chats.GetAsync(saga.ChatId, cancellationToken);

        Message message;
        try
        {
            if (chat is null)
                throw new NotFoundException($"Chat '{saga.ChatId}' no longer exists.");

            message = Message.SendWithAttachment(chat, saga.SenderUserId, saga.Content,
                new Attachment(reply.FileId, reply.FileName, reply.SizeBytes), now);
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            // E.g. the sender left the chat while FileService was working.
            await CompensateAsync(saga, ex.Message, now, cancellationToken);
            return;
        }

        saga.Complete(message.Id, now);
        if (!await sagas.CompleteWithMessageAsync(saga, FileMessageSagaStatus.AwaitingQuota, message, cancellationToken))
        {
            // The timeout job won the race and has already sent ReleaseQuota - the message was not saved.
            logger.Warning("QuotaReserved arrived while the saga was being timed out - message not saved.", new { saga.SagaId });
            return;
        }

        logger.Information("File message saga completed.", new { saga.SagaId, saga.ChatId, MessageId = message.Id, reply.FileId });

        // Last step and the only one nobody can undo: now the recipients see the message.
        await messageClient.TryPublishAsync(new MessageSent(
            message.Id, chat!.Id, saga.SenderUserId, message.Content?.Value, message.SentAt, Recipients(chat, saga.SenderUserId),
            new AttachmentInfo(reply.FileId, reply.FileName, reply.SizeBytes)), logger);
    }

    /// <summary>Step 2 failed. FileService has already deleted its temporary file, so there is nothing to compensate.</summary>
    public async Task HandleAsync(QuotaRejected reply, CancellationToken cancellationToken = default)
    {
        var saga = await sagas.GetAsync(reply.SagaId, cancellationToken);
        if (saga is not { Status: FileMessageSagaStatus.AwaitingQuota })
        {
            // Late or duplicate. If we were compensating, QuotaReleased still arrives and ends the saga.
            logger.Warning("QuotaRejected for a saga that is not waiting - ignored.", new { reply.SagaId, Status = saga?.Status.ToString() });
            return;
        }

        saga.Reject(reply.Reason, time.GetUtcNowInMicroseconds());
        if (await sagas.UpdateAsync(saga, FileMessageSagaStatus.AwaitingQuota, cancellationToken))
            logger.Information("File message saga failed - quota rejected.", new { saga.SagaId, saga.FileId });
    }

    /// <summary>The compensation is done - nothing is left behind in FileService.</summary>
    public async Task HandleAsync(QuotaReleased reply, CancellationToken cancellationToken = default)
    {
        var saga = await sagas.GetAsync(reply.SagaId, cancellationToken);
        if (saga is not { Status: FileMessageSagaStatus.Compensating })
        {
            logger.Information("QuotaReleased for a saga that is not compensating - ignored.", new { reply.SagaId, Status = saga?.Status.ToString() });
            return;
        }

        saga.CompleteCompensation(time.GetUtcNowInMicroseconds());
        if (await sagas.UpdateAsync(saga, FileMessageSagaStatus.Compensating, cancellationToken))
            logger.Information("File message saga failed - compensation completed.", new { saga.SagaId, saga.FileId });
    }

    /// <summary>
    /// Called by the timeout job. Delivery is at-most-once, so a command or a reply can be lost:
    /// a saga waiting for quota is compensated, and a compensation without an answer is retried a few times.
    /// </summary>
    public async Task TimeOutStaleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNowInMicroseconds();
        var stale = await sagas.GetStaleAsync(now - timeout, limit: 100, cancellationToken);

        foreach (var saga in stale)
        {
            if (saga.Status == FileMessageSagaStatus.AwaitingQuota)
            {
                await CompensateAsync(saga, "Timed out waiting for FileService.", now, cancellationToken);
                continue;
            }

            var retry = saga.RetryCompensation(now);
            if (!await sagas.UpdateAsync(saga, FileMessageSagaStatus.Compensating, cancellationToken))
                continue;

            if (retry)
            {
                logger.Warning("ReleaseQuota got no answer - retrying.", new { saga.SagaId, saga.FileId, saga.CompensationAttempts });
                await messageClient.TryPublishAsync(new ReleaseQuota(saga.SagaId, saga.FileId, saga.SenderUserId), logger);
            }
            else
            {
                // The compensation itself failed. We can't undo it automatically - alert an operator via the log.
                logger.Error("Compensation failed - the file and its quota may still be reserved in FileService. Manual cleanup needed.",
                    new { saga.SagaId, saga.FileId, saga.SenderUserId, saga.CompensationAttempts });
            }
        }
    }

    private async Task CompensateAsync(FileMessageSaga saga, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        saga.StartCompensation(reason, now);
        if (!await sagas.UpdateAsync(saga, FileMessageSagaStatus.AwaitingQuota, cancellationToken))
            return; // Someone else moved the saga on - they own the next step.

        logger.Warning("File message saga compensating.", new { saga.SagaId, saga.FileId, Reason = reason });
        await messageClient.TryPublishAsync(new ReleaseQuota(saga.SagaId, saga.FileId, saga.SenderUserId), logger);
    }

    /// <summary>
    /// The quota was reserved after we gave up (timeout) - release it again. ReleaseQuota is idempotent,
    /// so it does no harm if the release already happened. A duplicate for a completed saga is ignored.
    /// </summary>
    private async Task HandleLateReservationAsync(FileMessageSaga saga)
    {
        if (saga.Status == FileMessageSagaStatus.Completed)
        {
            logger.Information("Duplicate QuotaReserved for a completed saga - ignored.", new { saga.SagaId });
            return;
        }

        logger.Warning("Late QuotaReserved - releasing the quota again.", new { saga.SagaId, saga.FileId, Status = saga.Status.ToString() });
        await messageClient.TryPublishAsync(new ReleaseQuota(saga.SagaId, saga.FileId, saga.SenderUserId), logger);
    }

    private static List<Guid> Recipients(Chat chat, Guid senderUserId) =>
        chat.Participants
            .Where(p => p.IsActive && p.UserId != senderUserId)
            .Select(p => p.UserId)
            .ToList();
}

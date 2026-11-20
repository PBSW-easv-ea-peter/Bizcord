namespace ChatService.Domain;

public enum FileMessageSagaStatus
{
    /// <summary>ReserveQuota is sent - waiting for FileService.</summary>
    AwaitingQuota,

    /// <summary>The message is saved. End state.</summary>
    Completed,

    /// <summary>Quota was (or may have been) reserved, but the message could not be saved - ReleaseQuota is sent.</summary>
    Compensating,

    /// <summary>Nothing is left behind in any service. End state.</summary>
    Failed,

    /// <summary>ReleaseQuota got no answer after several attempts - an operator has to look. End state.</summary>
    CompensationFailed
}

/// <summary>
/// Saga state for "send a message with a file" (docs/documentation/saga-file-attachment.md).
/// ChatService is the orchestrator, so the state lives in ChatDB. The status transitions are the
/// only rules here - deciding which command to send next is the orchestrator's job.
/// </summary>
public sealed class FileMessageSaga
{
    public const int MaxCompensationAttempts = 3;

    public Guid SagaId { get; }
    public Guid ChatId { get; }
    public Guid SenderUserId { get; }
    public Guid FileId { get; }

    /// <summary>The text to send with the file - kept here until the message is saved.</summary>
    public MessageContent? Content { get; }

    public FileMessageSagaStatus Status { get; private set; }
    public Guid? MessageId { get; private set; }
    public string? FailureReason { get; private set; }
    public int CompensationAttempts { get; private set; }
    public DateTimeOffset StartedAt { get; }

    /// <summary>Last change - the timeout job compares with it.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>When the saga reached an end state (Completed, Failed or CompensationFailed). Null while it is running.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsFinished => Status is FileMessageSagaStatus.Completed or FileMessageSagaStatus.Failed or FileMessageSagaStatus.CompensationFailed;

    private FileMessageSaga(Guid sagaId, Guid chatId, Guid senderUserId, Guid fileId, MessageContent? content,
        FileMessageSagaStatus status, Guid? messageId, string? failureReason, int compensationAttempts,
        DateTimeOffset startedAt, DateTimeOffset updatedAt, DateTimeOffset? completedAt)
    {
        CompletedAt = completedAt;
        SagaId = sagaId;
        ChatId = chatId;
        SenderUserId = senderUserId;
        FileId = fileId;
        Content = content;
        Status = status;
        MessageId = messageId;
        FailureReason = failureReason;
        CompensationAttempts = compensationAttempts;
        StartedAt = startedAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Checks access up front, so a user who is not a participant gets 403 instead of a saga that fails later.</summary>
    public static FileMessageSaga Start(Chat chat, Guid senderUserId, Guid fileId, MessageContent? content, DateTimeOffset now)
    {
        if (!chat.CanSend(senderUserId))
            throw new NotAllowedException("Only active participants can send messages.");

        if (fileId == Guid.Empty)
            throw new DomainException("A file id is required.");

        return new FileMessageSaga(Guid.CreateVersion7(now), chat.Id, senderUserId, fileId, content,
            FileMessageSagaStatus.AwaitingQuota, null, null, 0, now, now, null);
    }

    internal static FileMessageSaga Restore(Guid sagaId, Guid chatId, Guid senderUserId, Guid fileId, MessageContent? content,
        FileMessageSagaStatus status, Guid? messageId, string? failureReason, int compensationAttempts,
        DateTimeOffset startedAt, DateTimeOffset updatedAt, DateTimeOffset? completedAt) =>
        new(sagaId, chatId, senderUserId, fileId, content, status, messageId, failureReason, compensationAttempts, startedAt, updatedAt, completedAt);

    /// <summary>Step 3 succeeded: the message is saved.</summary>
    public void Complete(Guid messageId, DateTimeOffset now)
    {
        EnsureStatus(FileMessageSagaStatus.AwaitingQuota);
        MessageId = messageId;
        End(FileMessageSagaStatus.Completed, now);
    }

    /// <summary>FileService said no. It has already deleted its temporary file, so there is nothing to compensate.</summary>
    public void Reject(string reason, DateTimeOffset now)
    {
        EnsureStatus(FileMessageSagaStatus.AwaitingQuota);
        FailureReason = reason;
        End(FileMessageSagaStatus.Failed, now);
    }

    /// <summary>A later step failed (or we gave up waiting) - the quota and the file must be released.</summary>
    public void StartCompensation(string reason, DateTimeOffset now)
    {
        EnsureStatus(FileMessageSagaStatus.AwaitingQuota);
        Status = FileMessageSagaStatus.Compensating;
        FailureReason = reason;
        CompensationAttempts = 1;
        UpdatedAt = now;
    }

    /// <summary>
    /// ReleaseQuota got no answer in time (delivery is at-most-once). Returns false when we give up -
    /// then the saga ends as CompensationFailed and an operator has to clean up.
    /// </summary>
    public bool RetryCompensation(DateTimeOffset now)
    {
        EnsureStatus(FileMessageSagaStatus.Compensating);

        if (CompensationAttempts >= MaxCompensationAttempts)
        {
            End(FileMessageSagaStatus.CompensationFailed, now);
            return false;
        }

        CompensationAttempts++;
        UpdatedAt = now;
        return true;
    }

    /// <summary>FileService confirmed the release.</summary>
    public void CompleteCompensation(DateTimeOffset now)
    {
        EnsureStatus(FileMessageSagaStatus.Compensating);
        End(FileMessageSagaStatus.Failed, now);
    }

    private void End(FileMessageSagaStatus endState, DateTimeOffset now)
    {
        Status = endState;
        UpdatedAt = now;
        CompletedAt = now;
    }

    private void EnsureStatus(FileMessageSagaStatus expected)
    {
        if (Status != expected)
            throw new ConflictException($"Saga '{SagaId}' is {Status}, expected {expected}.");
    }
}

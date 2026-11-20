using ChatService.Application;
using ChatService.Domain;
using Dapper;

namespace ChatService.Infrastructure.Persistence;

public sealed class DapperFileMessageSagaRepository(ChatDb db) : IFileMessageSagaRepository
{
    private const string SelectColumns =
        "select saga_id as SagaId, chat_id as ChatId, sender_user_id as SenderUserId, file_id as FileId, content, status, " +
        "message_id as MessageId, failure_reason as FailureReason, compensation_attempts as CompensationAttempts, " +
        "started_at as StartedAt, updated_at as UpdatedAt, completed_at as CompletedAt from file_message_sagas";

    private const string UpdateSql =
        """
        update file_message_sagas
        set status = @Status, message_id = @MessageId, failure_reason = @FailureReason,
            compensation_attempts = @CompensationAttempts, updated_at = @UpdatedAt, completed_at = @CompletedAt
        where saga_id = @SagaId and status = @ExpectedStatus
        """;

    public async Task<FileMessageSaga?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        var row = await db.RunAsync(connection => connection.QuerySingleOrDefaultAsync<SagaRow>(new CommandDefinition(
            $"{SelectColumns} where saga_id = @SagaId",
            new { SagaId = sagaId }, cancellationToken: cancellationToken)), cancellationToken);

        return row is null ? null : ToDomain(row);
    }

    public Task AddAsync(FileMessageSaga saga, CancellationToken cancellationToken = default) =>
        db.RunAsync(connection => connection.ExecuteAsync(new CommandDefinition(
            """
            insert into file_message_sagas (saga_id, chat_id, sender_user_id, file_id, content, status,
                                            message_id, failure_reason, compensation_attempts, started_at, updated_at, completed_at)
            values (@SagaId, @ChatId, @SenderUserId, @FileId, @Content, @Status,
                    @MessageId, @FailureReason, @CompensationAttempts, @StartedAt, @UpdatedAt, @CompletedAt)
            """,
            new
            {
                saga.SagaId,
                saga.ChatId,
                saga.SenderUserId,
                saga.FileId,
                Content = saga.Content?.Value,
                Status = saga.Status.ToString(),
                saga.MessageId,
                saga.FailureReason,
                saga.CompensationAttempts,
                StartedAt = DbTime.ToDb(saga.StartedAt),
                UpdatedAt = DbTime.ToDb(saga.UpdatedAt),
                CompletedAt = DbTime.ToDb(saga.CompletedAt)
            },
            cancellationToken: cancellationToken)), cancellationToken);

    public async Task<bool> UpdateAsync(FileMessageSaga saga, FileMessageSagaStatus expectedStatus, CancellationToken cancellationToken = default)
    {
        var updated = await db.RunAsync(connection => connection.ExecuteAsync(new CommandDefinition(
            UpdateSql, UpdateParameters(saga, expectedStatus), cancellationToken: cancellationToken)), cancellationToken);

        return updated == 1;
    }

    public Task<bool> CompleteWithMessageAsync(FileMessageSaga saga, FileMessageSagaStatus expectedStatus, Message message, CancellationToken cancellationToken = default) =>
        db.RunAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            // Saga first: if the timeout job has already started compensating, no message may be saved.
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                UpdateSql, UpdateParameters(saga, expectedStatus), transaction, cancellationToken: cancellationToken));

            if (updated != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await DapperMessageRepository.InsertAsync(connection, transaction, message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public async Task<IReadOnlyList<FileMessageSaga>> GetStaleAsync(DateTimeOffset notUpdatedSince, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await db.RunAsync(connection => connection.QueryAsync<SagaRow>(new CommandDefinition(
            $"""
             {SelectColumns}
             where status in ('AwaitingQuota', 'Compensating') and updated_at < @NotUpdatedSince
             order by updated_at
             limit @Limit
             """,
            new { NotUpdatedSince = DbTime.ToDb(notUpdatedSince), Limit = limit },
            cancellationToken: cancellationToken)), cancellationToken);

        return rows.Select(ToDomain).ToList();
    }

    private static object UpdateParameters(FileMessageSaga saga, FileMessageSagaStatus expectedStatus) => new
    {
        saga.SagaId,
        Status = saga.Status.ToString(),
        saga.MessageId,
        saga.FailureReason,
        saga.CompensationAttempts,
        UpdatedAt = DbTime.ToDb(saga.UpdatedAt),
        CompletedAt = DbTime.ToDb(saga.CompletedAt),
        ExpectedStatus = expectedStatus.ToString()
    };

    private static FileMessageSaga ToDomain(SagaRow row) => FileMessageSaga.Restore(
        row.SagaId,
        row.ChatId,
        row.SenderUserId,
        row.FileId,
        row.Content is null ? null : new MessageContent(row.Content),
        Enum.Parse<FileMessageSagaStatus>(row.Status),
        row.MessageId,
        row.FailureReason,
        row.CompensationAttempts,
        DbTime.FromDb(row.StartedAt),
        DbTime.FromDb(row.UpdatedAt),
        DbTime.FromDb(row.CompletedAt));

    private sealed class SagaRow
    {
        public Guid SagaId { get; init; }
        public Guid ChatId { get; init; }
        public Guid SenderUserId { get; init; }
        public Guid FileId { get; init; }
        public string? Content { get; init; }
        public string Status { get; init; } = "";
        public Guid? MessageId { get; init; }
        public string? FailureReason { get; init; }
        public int CompensationAttempts { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}

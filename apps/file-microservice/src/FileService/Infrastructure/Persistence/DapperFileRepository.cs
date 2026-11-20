using Dapper;
using FileService.Application;
using FileService.Domain;
using Npgsql;

namespace FileService.Infrastructure.Persistence;

public sealed class DapperFileRepository(NpgsqlDataSource dataSource) : IFileRepository
{
    private const string SelectFile =
        "select id, owner_user_id as OwnerUserId, file_name as FileName, content_type as ContentType, " +
        "size_bytes as SizeBytes, status, saga_id as SagaId, uploaded_at as UploadedAt from files";

    public async Task AddTemporaryAsync(StoredFile file, byte[] content, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into files (id, owner_user_id, file_name, content_type, size_bytes, content, status, saga_id, uploaded_at)
            values (@Id, @OwnerUserId, @FileName, @ContentType, @SizeBytes, @Content, 'Temporary', null, @UploadedAt)
            """,
            new
            {
                file.Id,
                file.OwnerUserId,
                file.FileName,
                file.ContentType,
                file.SizeBytes,
                Content = content,
                UploadedAt = file.UploadedAt.ToUniversalTime()
            },
            cancellationToken: cancellationToken));
    }

    public async Task<StoredFile?> GetAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<FileRow>(new CommandDefinition(
            $"{SelectFile} where id = @FileId", new { FileId = fileId }, cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<byte[]?> GetContentAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<byte[]>(new CommandDefinition(
            "select content from files where id = @FileId", new { FileId = fileId }, cancellationToken: cancellationToken));
    }

    public async Task<UserQuota> GetQuotaAsync(Guid userId, long standardLimitBytes, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<UserQuota>(new CommandDefinition(
                   "select user_id as UserId, tier, limit_bytes as LimitBytes, used_bytes as UsedBytes from user_quotas where user_id = @UserId",
                   new { UserId = userId }, cancellationToken: cancellationToken))
               ?? new UserQuota(userId, "Standard", standardLimitBytes, 0);
    }

    public async Task<ReservationResult> ReserveQuotaAsync(Guid sagaId, Guid fileId, Guid userId, long standardLimitBytes, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Locks the file row, so a duplicate ReserveQuota or a ReleaseQuota for the same file waits for us.
        var file = (await connection.QuerySingleOrDefaultAsync<FileRow>(new CommandDefinition(
            $"{SelectFile} where id = @FileId for update", new { FileId = fileId }, transaction, cancellationToken: cancellationToken)))?.ToDomain();

        if (file is null || file.OwnerUserId != userId)
            return await Finish(new ReservationResult(ReservationOutcome.NotAvailable, null, null));

        if (file.Status == FileStatus.Attached)
            return await Finish(new ReservationResult(
                file.SagaId == sagaId ? ReservationOutcome.AlreadyReserved : ReservationOutcome.NotAvailable, file, null));

        // First reservation for a user without a row: they are on the standard tier.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into user_quotas (user_id, tier, limit_bytes, used_bytes)
            values (@UserId, 'Standard', @Limit, 0)
            on conflict (user_id) do nothing
            """,
            new { UserId = userId, Limit = standardLimitBytes }, transaction, cancellationToken: cancellationToken));

        // Check and deduct in one statement - two concurrent reservations can't both squeeze in under the limit.
        var deducted = await connection.ExecuteAsync(new CommandDefinition(
            """
            update user_quotas set used_bytes = used_bytes + @Size
            where user_id = @UserId and used_bytes + @Size <= limit_bytes
            """,
            new { UserId = userId, Size = file.SizeBytes }, transaction, cancellationToken: cancellationToken));

        if (deducted == 0)
        {
            var quota = await GetQuotaAsync(connection, transaction, userId, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                "delete from files where id = @FileId", new { FileId = fileId }, transaction, cancellationToken: cancellationToken));

            return await Finish(new ReservationResult(ReservationOutcome.QuotaExceeded, file, quota));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "update files set status = 'Attached', saga_id = @SagaId where id = @FileId",
            new { SagaId = sagaId, FileId = fileId }, transaction, cancellationToken: cancellationToken));

        return await Finish(new ReservationResult(ReservationOutcome.Reserved, file with { Status = FileStatus.Attached, SagaId = sagaId }, null));

        async Task<ReservationResult> Finish(ReservationResult result)
        {
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }

    public async Task<bool> ReleaseAsync(Guid sagaId, Guid fileId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var file = (await connection.QuerySingleOrDefaultAsync<FileRow>(new CommandDefinition(
            $"{SelectFile} where id = @FileId for update", new { FileId = fileId }, transaction, cancellationToken: cancellationToken)))?.ToDomain();

        // Already released, someone else's file, or attached by another saga: not ours to touch.
        if (file is null || file.OwnerUserId != userId || (file.Status == FileStatus.Attached && file.SagaId != sagaId))
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        if (file.Status == FileStatus.Attached)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "update user_quotas set used_bytes = used_bytes - @Size where user_id = @UserId",
                new { UserId = userId, Size = file.SizeBytes }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "delete from files where id = @FileId", new { FileId = fileId }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<UserQuota> GetQuotaAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, CancellationToken cancellationToken) =>
        await connection.QuerySingleAsync<UserQuota>(new CommandDefinition(
            "select user_id as UserId, tier, limit_bytes as LimitBytes, used_bytes as UsedBytes from user_quotas where user_id = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));

    private sealed class FileRow
    {
        public Guid Id { get; init; }
        public Guid OwnerUserId { get; init; }
        public string FileName { get; init; } = "";
        public string ContentType { get; init; } = "";
        public long SizeBytes { get; init; }
        public string Status { get; init; } = "";
        public Guid? SagaId { get; init; }
        public DateTime UploadedAt { get; init; }

        public StoredFile ToDomain() => new(
            Id, OwnerUserId, FileName, ContentType, SizeBytes, Enum.Parse<FileStatus>(Status), SagaId,
            new DateTimeOffset(DateTime.SpecifyKind(UploadedAt, DateTimeKind.Utc)));
    }
}

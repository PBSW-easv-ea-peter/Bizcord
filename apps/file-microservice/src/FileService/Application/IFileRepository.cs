using FileService.Domain;

namespace FileService.Application;

public enum ReservationOutcome
{
    Reserved,

    /// <summary>The same saga already reserved this file - a duplicate command. Answer Reserved again.</summary>
    AlreadyReserved,

    /// <summary>Quota exceeded - the temporary file has been deleted in the same transaction.</summary>
    QuotaExceeded,

    /// <summary>Unknown file, another user's file, or attached to another saga. Nothing was changed.</summary>
    NotAvailable
}

public sealed record ReservationResult(ReservationOutcome Outcome, StoredFile? File, UserQuota? Quota);

public interface IFileRepository
{
    Task AddTemporaryAsync(StoredFile file, byte[] content, CancellationToken cancellationToken = default);

    Task<StoredFile?> GetAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<byte[]?> GetContentAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>Users without a row get the standard tier with <paramref name="standardLimitBytes"/>.</summary>
    Task<UserQuota> GetQuotaAsync(Guid userId, long standardLimitBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saga step 2 as one local transaction: deduct the quota and attach the file - or, if the quota is exceeded,
    /// delete the temporary file (compensating step 1 locally). Safe against concurrent uploads by the same user.
    /// </summary>
    Task<ReservationResult> ReserveQuotaAsync(Guid sagaId, Guid fileId, Guid userId, long standardLimitBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compensation as one local transaction: give the quota back (if this saga reserved it) and delete the file.
    /// Idempotent - returns false if there was nothing to release.
    /// </summary>
    Task<bool> ReleaseAsync(Guid sagaId, Guid fileId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The message the file was attached to is deleted: delete the file and give its size back to the owner's quota,
    /// in one local transaction. Idempotent - returns false if the file is already gone (or was never attached).
    /// </summary>
    Task<bool> DeleteAttachedAsync(Guid fileId, CancellationToken cancellationToken = default);
}

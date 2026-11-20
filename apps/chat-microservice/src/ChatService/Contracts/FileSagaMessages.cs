namespace ChatService.Contracts;

// ChatService's own copy of the file-message saga messages (no shared code - see docs/contracts.md).
// Every message carries SagaId, so a reply can be matched to its saga instance.

/// <summary>Command: check the user's quota and deduct the file's size. The reply is QuotaReserved or QuotaRejected.</summary>
public sealed record ReserveQuota(
    Guid SagaId,
    Guid FileId,
    Guid UserId);

/// <summary>Compensation command: give the quota back and delete the file. Idempotent. The reply is QuotaReleased.</summary>
public sealed record ReleaseQuota(
    Guid SagaId,
    Guid FileId,
    Guid UserId);

/// <summary>Our reading of FileService's file.quota-reserved (tolerant reader - only the fields we use).</summary>
public sealed record QuotaReserved(
    Guid SagaId,
    Guid FileId,
    string FileName,
    long SizeBytes);

public sealed record QuotaRejected(
    Guid SagaId,
    Guid FileId,
    string Reason);

public sealed record QuotaReleased(
    Guid SagaId,
    Guid FileId);

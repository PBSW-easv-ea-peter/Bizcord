namespace FileService.Contracts;

/// <summary>
/// Language-neutral message names - they are the contract, not the C# type names. FileService owns them:
/// the commands are addressed to FileService, the events come from it. See docs/contracts.md.
/// </summary>
public static class MessageNames
{
    /// <summary>Consumes (command).</summary>
    public const string ReserveQuota = "file.reserve-quota";

    /// <summary>Consumes (compensating command).</summary>
    public const string ReleaseQuota = "file.release-quota";

    public const string QuotaReserved = "file.quota-reserved";
    public const string QuotaRejected = "file.quota-rejected";
    public const string QuotaReleased = "file.quota-released";
}

// Every message carries SagaId - that is how the orchestrator matches a reply to its saga instance.

/// <summary>Command: check the user's quota and deduct the file's size.</summary>
public sealed record ReserveQuota(
    Guid SagaId,
    Guid FileId,
    Guid UserId);

/// <summary>Compensating command: give the quota back and delete the file. Idempotent.</summary>
public sealed record ReleaseQuota(
    Guid SagaId,
    Guid FileId,
    Guid UserId);

/// <summary>The quota is deducted and the file is attached. Name and size let ChatService show the file without calling us.</summary>
public sealed record QuotaReserved(
    Guid SagaId,
    Guid FileId,
    Guid UserId,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>Nothing was reserved, and the temporary file is already deleted - there is nothing to compensate.</summary>
public sealed record QuotaRejected(
    Guid SagaId,
    Guid FileId,
    Guid UserId,
    string Reason);

/// <summary>The compensation is done (or there was nothing to release).</summary>
public sealed record QuotaReleased(
    Guid SagaId,
    Guid FileId,
    Guid UserId);

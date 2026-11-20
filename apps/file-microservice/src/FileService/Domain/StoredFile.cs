namespace FileService.Domain;

public enum FileStatus
{
    /// <summary>Uploaded, but no quota is reserved yet. Deleted again if the saga fails.</summary>
    Temporary,

    /// <summary>Quota is reserved by a saga, and the file belongs to a message.</summary>
    Attached
}

/// <summary>A file's metadata. The bytes are read separately - they are only needed for download.</summary>
public sealed record StoredFile(
    Guid Id,
    Guid OwnerUserId,
    string FileName,
    string ContentType,
    long SizeBytes,
    FileStatus Status,
    Guid? SagaId,
    DateTimeOffset UploadedAt)
{
    public const int MaxFileNameLength = 255;

    /// <summary>Keeps only the name - a client may send a full path, which must not end up in the DB or in a download header.</summary>
    public static string CleanFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        if (name.Length == 0)
            throw new FileDomainException("The file needs a name.");

        return name.Length <= MaxFileNameLength ? name : name[..MaxFileNameLength];
    }
}

/// <summary>A user's storage. Premium users get a bigger limit than standard users.</summary>
public sealed record UserQuota(Guid UserId, string Tier, long LimitBytes, long UsedBytes)
{
    public long RemainingBytes => LimitBytes - UsedBytes;
}

public sealed class FileDomainException(string message) : Exception(message);

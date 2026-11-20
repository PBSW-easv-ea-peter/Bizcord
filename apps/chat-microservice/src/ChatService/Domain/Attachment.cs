namespace ChatService.Domain;

/// <summary>
/// A file attached to a message. The file itself is owned by FileService - ChatService only keeps
/// the reference and what is needed to show it (name and size), copied from file.quota-reserved.
/// </summary>
public sealed record Attachment
{
    public const int MaxFileNameLength = 255;

    public Guid FileId { get; }
    public string FileName { get; }
    public long SizeBytes { get; }

    public Attachment(Guid fileId, string fileName, long sizeBytes)
    {
        if (fileId == Guid.Empty)
            throw new DomainException("An attachment must reference a file.");

        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > MaxFileNameLength)
            throw new DomainException($"An attachment needs a file name of 1-{MaxFileNameLength} characters.");

        if (sizeBytes <= 0)
            throw new DomainException("An attachment cannot be empty.");

        FileId = fileId;
        FileName = fileName;
        SizeBytes = sizeBytes;
    }
}

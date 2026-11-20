namespace FileService.Contracts;

/// <summary>Use FileId in ChatService's POST /chats/{chatId}/file-messages.</summary>
public sealed record FileResponse(
    Guid FileId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    DateTimeOffset UploadedAt);

public sealed record QuotaResponse(
    Guid UserId,
    string Tier,
    long LimitBytes,
    long UsedBytes,
    long RemainingBytes);

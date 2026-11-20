namespace FileService.Contracts;

// FileService's own reading of ChatService's events - only the fields we use (tolerant reader).
// See apps/chat-microservice/docs/contracts.md.

public static class ChatEventNames
{
    public const string MessageDeleted = "chat.message-deleted";
}

/// <summary>AttachmentFileId is null for messages without a file - then there is nothing for us to do.</summary>
public sealed record MessageDeleted(
    Guid MessageId,
    Guid? AttachmentFileId);

using Bizcord.Logging;
using FileService.Contracts;

namespace FileService.Application;

/// <summary>
/// Gives the quota back when a message with a file is deleted in ChatService. Not part of the saga:
/// a deletion can't be refused, so there is nothing to compensate - we just react to the event.
/// If the event is lost (at-most-once), the space stays used. We accept that.
/// </summary>
public sealed class MessageDeletedHandler(IFileRepository files, ILogger<MessageDeletedHandler> logger)
{
    public async Task HandleAsync(MessageDeleted message, CancellationToken cancellationToken = default)
    {
        if (message.AttachmentFileId is not { } fileId)
            return;

        var deleted = await files.DeleteAttachedAsync(fileId, cancellationToken);

        logger.Information(deleted ? "Message deleted - file deleted and quota freed." : "Message deleted - file already gone.",
            new { message.MessageId, FileId = fileId });
    }
}

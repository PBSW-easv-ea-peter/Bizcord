using ChatService.Contracts;
using ChatService.Domain;

namespace ChatService.Controllers;

/// <summary>Domain → contract. Lives in the API layer, so neither Domain nor Contracts know about each other.</summary>
internal static class ContractMappings
{
    public static ChatResponse ToResponse(this Chat chat) => new(
        chat.Id,
        chat.Type.ToString(),
        chat.Title?.Value,
        chat.CreatedAt,
        chat.Participants.Select(p => p.ToResponse()).ToList());

    public static ParticipantResponse ToResponse(this ChatParticipant participant) => new(
        participant.UserId,
        participant.Role.ToString(),
        participant.JoinedAt,
        participant.LeftAt);

    public static MessageResponse ToResponse(this Message message) => new(
        message.Id,
        message.ChatId,
        message.SenderUserId,
        message.Content?.Value,
        message.SentAt,
        message.EditedAt,
        message.DeletedAt,
        message.Attachment is { } attachment
            ? new AttachmentResponse(attachment.FileId, attachment.FileName, attachment.SizeBytes)
            : null);

    public static FileMessageResponse ToResponse(this FileMessageSaga saga) => new(
        saga.SagaId,
        saga.ChatId,
        saga.FileId,
        saga.Status.ToString(),
        saga.MessageId,
        saga.FailureReason,
        saga.StartedAt,
        saga.CompletedAt);

    public static ReceiptResponse ToResponse(this MessageReceipt receipt) => new(
        receipt.UserId,
        receipt.DeliveredAt,
        receipt.SeenAt);
}

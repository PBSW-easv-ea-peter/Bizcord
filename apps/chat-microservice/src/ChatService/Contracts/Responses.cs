namespace ChatService.Contracts;

// Shared model: only what others need. Internal ids (e.g. participant id) are not exposed.

public sealed record ChatResponse(
    Guid Id,
    string Type,
    string? Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ParticipantResponse> Participants);

public sealed record ParticipantResponse(
    Guid UserId,
    string Role,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LeftAt);

public sealed record MessageResponse(
    Guid Id,
    Guid ChatId,
    Guid SenderUserId,
    string? Content,
    DateTimeOffset SentAt,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt,
    AttachmentResponse? Attachment = null);

/// <summary>Download the file from FileService: GET /files/{fileId}.</summary>
public sealed record AttachmentResponse(
    Guid FileId,
    string FileName,
    long SizeBytes);

/// <summary>
/// Status of a "send message with file" saga. Status is AwaitingQuota, Completed, Compensating, Failed or CompensationFailed.
/// MessageId is set when Completed, Reason when it failed, CompletedAt once it has ended.
/// </summary>
public sealed record FileMessageResponse(
    Guid SagaId,
    Guid ChatId,
    Guid FileId,
    string Status,
    Guid? MessageId,
    string? Reason,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record ReceiptResponse(
    Guid UserId,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? SeenAt);

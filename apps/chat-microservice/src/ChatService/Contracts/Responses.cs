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

public sealed record ReceiptResponse(
    Guid UserId,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? SeenAt);

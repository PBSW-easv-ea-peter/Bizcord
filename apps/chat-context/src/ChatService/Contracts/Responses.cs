namespace ChatService.Contracts;

// Delt model: kun hvad andre har brug for. Interne id'er (fx deltager-id) eksponeres ikke.

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
    string Content,
    DateTimeOffset SentAt);

public sealed record ReceiptResponse(
    Guid UserId,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? SeenAt);

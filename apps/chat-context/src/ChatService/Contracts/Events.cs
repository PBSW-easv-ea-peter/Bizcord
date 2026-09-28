namespace ChatService.Contracts;

/// <summary>
/// Sprogneutrale event-navne - de er kontrakten, ikke C#-typenavnene.
/// Bliver både exchange-navn og 'type'-header i RabbitMQ. Se docs/contracts.md.
/// </summary>
public static class EventNames
{
    public const string MessageSent = "chat.message-sent";
    public const string ParticipantAdded = "chat.participant-added";
    public const string MessagesSeen = "chat.messages-seen";
}

public sealed record MessageSent(
    Guid MessageId,
    Guid ChatId,
    Guid SenderUserId,
    string Content,
    DateTimeOffset SentAt,
    IReadOnlyList<Guid> RecipientUserIds);

public sealed record ParticipantAdded(
    Guid ChatId,
    Guid UserId,
    string Role,
    Guid AddedByUserId,
    DateTimeOffset JoinedAt);

/// <summary>Én pr. "til og med"-batch: alle andres beskeder til og med UpToMessageId er nu set af UserId.</summary>
public sealed record MessagesSeen(
    Guid ChatId,
    Guid UserId,
    Guid UpToMessageId,
    DateTimeOffset SeenAt);

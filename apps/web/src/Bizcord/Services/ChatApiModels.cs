namespace Bizcord.Services;

// The frontend's own copy of the ChatService responses it reads (tolerant reader, no shared code with ChatService).
// Only the fields the pages use; unknown fields in the JSON are ignored.
// Contract: apps/chat-microservice/docs/contracts.md

public sealed record ChatDto(
    Guid Id,
    string Type,
    string? Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ParticipantDto> Participants)
{
    public bool IsGroup => Type == "Group";

    public IEnumerable<ParticipantDto> ActiveParticipants => Participants.Where(p => p.LeftAt is null);

    public bool HasActiveParticipant(Guid userId) => ActiveParticipants.Any(p => p.UserId == userId);
}

public sealed record ParticipantDto(Guid UserId, string Role, DateTimeOffset? LeftAt);

public sealed record MessageDto(
    Guid Id,
    Guid SenderUserId,
    string? Content,
    DateTimeOffset SentAt,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt,
    AttachmentDto? Attachment)
{
    public bool IsDeleted => DeletedAt is not null;
    public bool IsEdited => EditedAt is not null;
}

public sealed record AttachmentDto(Guid FileId, string FileName, long SizeBytes);

/// <summary>One page of messages, oldest first (the order the chat shows them in).</summary>
public sealed record MessagePage(IReadOnlyList<MessageDto> Messages, bool HasOlder);

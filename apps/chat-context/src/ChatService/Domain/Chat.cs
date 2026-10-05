namespace ChatService.Domain;

/// <summary>Aggregate root for a chat and its participants.</summary>
public sealed class Chat
{
    private readonly List<ChatParticipant> _participants;

    public Guid Id { get; }
    public ChatType Type { get; }
    public ChatTitle? Title { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<ChatParticipant> Participants => _participants;

    private Chat(Guid id, ChatType type, ChatTitle? title, DateTimeOffset createdAt, List<ChatParticipant> participants)
    {
        Id = id;
        Type = type;
        Title = title;
        CreatedAt = createdAt;
        _participants = participants;
    }

    public static Chat CreateDirect(Guid userA, Guid userB, DateTimeOffset now)
    {
        if (userA == userB)
            throw new DomainException("A direct chat requires two different users.");

        return new Chat(Guid.CreateVersion7(now), ChatType.Direct, null, now,
        [
            new ChatParticipant(Guid.CreateVersion7(now), userA, ParticipantRole.Member, now),
            new ChatParticipant(Guid.CreateVersion7(now), userB, ParticipantRole.Member, now)
        ]);
    }

    public static Chat CreateGroup(Guid creatorUserId, ChatTitle title, DateTimeOffset now)
    {
        return new Chat(Guid.CreateVersion7(now), ChatType.Group, title, now,
        [
            new ChatParticipant(Guid.CreateVersion7(now), creatorUserId, ParticipantRole.Owner, now)
        ]);
    }

    /// <summary>Rehydration from persistence - does not run creation invariants.</summary>
    internal static Chat Restore(Guid id, ChatType type, ChatTitle? title, DateTimeOffset createdAt, IEnumerable<ChatParticipant> participants) =>
        new(id, type, title, createdAt, participants.ToList());

    public ChatParticipant AddParticipant(Guid requestedByUserId, Guid userId, DateTimeOffset now)
    {
        if (Type == ChatType.Direct)
            throw new DomainException("Participants cannot be added to a direct chat.");

        var requester = FindActive(requestedByUserId);
        if (requester is null || requester.Role == ParticipantRole.Member)
            throw new NotAllowedException("Only an owner or admin can add participants.");

        if (_participants.Any(p => p.UserId == userId))
            throw new ConflictException("User is already a participant.");

        var participant = new ChatParticipant(Guid.CreateVersion7(now), userId, ParticipantRole.Member, now);
        _participants.Add(participant);
        return participant;
    }

    public bool CanSend(Guid userId) => FindActive(userId) is not null;

    // Same rule as CanSend today, but a different intent - can change independently.
    public bool CanRead(Guid userId) => FindActive(userId) is not null;

    private ChatParticipant? FindActive(Guid userId) =>
        _participants.FirstOrDefault(p => p.UserId == userId && p.IsActive);
}

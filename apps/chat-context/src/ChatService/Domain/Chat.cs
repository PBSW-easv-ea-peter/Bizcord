namespace ChatService.Domain;

/// <summary>Aggregat-rod for en chat og dens deltagere.</summary>
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

    /// <summary>Genopbygning fra persistens - kører ikke oprettelses-invarianter.</summary>
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

    // Samme regel som CanSend i dag, men en anden intention - kan ændres uafhængigt.
    public bool CanRead(Guid userId) => FindActive(userId) is not null;

    private ChatParticipant? FindActive(Guid userId) =>
        _participants.FirstOrDefault(p => p.UserId == userId && p.IsActive);
}

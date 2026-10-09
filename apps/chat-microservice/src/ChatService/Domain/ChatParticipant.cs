namespace ChatService.Domain;

/// <summary>Part of the Chat aggregate - created only via Chat.</summary>
public sealed class ChatParticipant
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public ParticipantRole Role { get; }
    public DateTimeOffset JoinedAt { get; }
    public DateTimeOffset? LeftAt { get; }

    public bool IsActive => LeftAt is null;

    internal ChatParticipant(Guid id, Guid userId, ParticipantRole role, DateTimeOffset joinedAt, DateTimeOffset? leftAt = null)
    {
        Id = id;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        LeftAt = leftAt;
    }
}

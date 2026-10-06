using ChatService.Domain;

namespace ChatService.Tests.Domain;

public class ChatTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDirect_WithTwoUsers_BothAreMembersAndNoTitle()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var chat = Chat.CreateDirect(userA, userB, Now);

        Assert.Equal(ChatType.Direct, chat.Type);
        Assert.Null(chat.Title);
        Assert.Equal(2, chat.Participants.Count);
        Assert.All(chat.Participants, p => Assert.Equal(ParticipantRole.Member, p.Role));
    }

    [Fact]
    public void CreateDirect_WithSameUserTwice_Throws()
    {
        var user = Guid.NewGuid();

        Assert.Throws<DomainException>(() => Chat.CreateDirect(user, user, Now));
    }

    [Fact]
    public void CreateGroup_CreatorBecomesOwner()
    {
        var creator = Guid.NewGuid();

        var chat = Chat.CreateGroup(creator, new ChatTitle("Team"), Now);

        var participant = Assert.Single(chat.Participants);
        Assert.Equal(creator, participant.UserId);
        Assert.Equal(ParticipantRole.Owner, participant.Role);
    }

    [Fact]
    public void AddParticipant_ByOwner_AddsMember()
    {
        var owner = Guid.NewGuid();
        var newUser = Guid.NewGuid();
        var chat = Chat.CreateGroup(owner, new ChatTitle("Team"), Now);

        var participant = chat.AddParticipant(owner, newUser, Now);

        Assert.Equal(ParticipantRole.Member, participant.Role);
        Assert.Contains(chat.Participants, p => p.UserId == newUser);
    }

    [Fact]
    public void AddParticipant_ByMember_Throws()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var chat = Chat.CreateGroup(owner, new ChatTitle("Team"), Now);
        chat.AddParticipant(owner, member, Now);

        Assert.Throws<NotAllowedException>(() => chat.AddParticipant(member, Guid.NewGuid(), Now));
    }

    [Fact]
    public void AddParticipant_ByNonParticipant_Throws()
    {
        var chat = Chat.CreateGroup(Guid.NewGuid(), new ChatTitle("Team"), Now);

        Assert.Throws<NotAllowedException>(() => chat.AddParticipant(Guid.NewGuid(), Guid.NewGuid(), Now));
    }

    [Fact]
    public void AddParticipant_Duplicate_Throws()
    {
        var owner = Guid.NewGuid();
        var user = Guid.NewGuid();
        var chat = Chat.CreateGroup(owner, new ChatTitle("Team"), Now);
        chat.AddParticipant(owner, user, Now);

        Assert.Throws<ConflictException>(() => chat.AddParticipant(owner, user, Now));
    }

    [Fact]
    public void AddParticipant_ToDirectChat_Throws()
    {
        var userA = Guid.NewGuid();
        var chat = Chat.CreateDirect(userA, Guid.NewGuid(), Now);

        Assert.Throws<DomainException>(() => chat.AddParticipant(userA, Guid.NewGuid(), Now));
    }

    [Fact]
    public void CanSend_OnlyForParticipants()
    {
        var userA = Guid.NewGuid();
        var chat = Chat.CreateDirect(userA, Guid.NewGuid(), Now);

        Assert.True(chat.CanSend(userA));
        Assert.False(chat.CanSend(Guid.NewGuid()));
    }
}

using ChatService.Application;
using ChatService.Contracts;
using ChatService.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChatService.Tests.Application;

public class ChatAppServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryChatRepository _chats = new();
    private readonly InMemoryMessageClient _messageClient = new();
    private readonly ChatAppService _service;

    public ChatAppServiceTests()
    {
        _service = CreateService(_messageClient, Now);
    }

    private ChatAppService CreateService(IMessageClient messageClient, DateTimeOffset now) =>
        new(_chats, messageClient, new FixedTimeProvider(now), NullLogger<ChatAppService>.Instance);

    [Fact]
    public async Task AddParticipant_PublishesParticipantAdded()
    {
        var owner = Guid.NewGuid();
        var user = Guid.NewGuid();
        var chat = await _service.CreateGroupAsync(owner, "Team");

        await _service.AddParticipantAsync(chat.Id, owner, user);

        var published = Assert.IsType<ParticipantAdded>(Assert.Single(_messageClient.Published));
        Assert.Equal(new ParticipantAdded(chat.Id, user, "Member", owner, Now), published);
    }

    [Fact]
    public async Task AddParticipant_WhenBrokerFails_StillSucceeds()
    {
        var owner = Guid.NewGuid();
        var service = CreateService(new FailingMessageClient(), Now);
        var chat = await service.CreateGroupAsync(owner, "Team");

        var participant = await service.AddParticipantAsync(chat.Id, owner, Guid.NewGuid());

        Assert.Same(participant, Assert.Single(_chats.AddedParticipants));
    }

    [Fact]
    public async Task CreateDirect_New_IsSavedAndMarkedCreated()
    {
        var (chat, created) = await _service.CreateDirectAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(created);
        Assert.Same(chat, _chats.Chats[chat.Id]);
    }

    [Fact]
    public async Task CreateDirect_WhenExists_ReturnsExistingRegardlessOfOrder()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var (first, _) = await _service.CreateDirectAsync(userA, userB);

        var (second, created) = await _service.CreateDirectAsync(userB, userA);

        Assert.False(created);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(_chats.Chats);
    }

    [Fact]
    public async Task CreateDirect_LosesRace_ReturnsWinner()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var winner = Chat.CreateDirect(userA, userB, Now);
        _chats.RaceWinner = winner;

        var (chat, created) = await _service.CreateDirectAsync(userA, userB);

        Assert.False(created);
        Assert.Equal(winner.Id, chat.Id);
    }

    [Fact]
    public async Task Get_ByNonParticipant_ThrowsNotAllowed()
    {
        var chat = await _service.CreateGroupAsync(Guid.NewGuid(), "Team");

        await Assert.ThrowsAsync<NotAllowedException>(() => _service.GetAsync(chat.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateGroup_InvalidTitle_ThrowsDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateGroupAsync(Guid.NewGuid(), "  "));
        Assert.Empty(_chats.Chats);
    }

    [Fact]
    public async Task AddParticipant_UnknownChat_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddParticipantAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task AddParticipant_ByOwner_IsSaved()
    {
        var owner = Guid.NewGuid();
        var chat = await _service.CreateGroupAsync(owner, "Team");

        var participant = await _service.AddParticipantAsync(chat.Id, owner, Guid.NewGuid());

        Assert.Same(participant, Assert.Single(_chats.AddedParticipants));
    }

    [Fact]
    public async Task AddParticipant_ByMember_ThrowsNotAllowed()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var chat = await _service.CreateGroupAsync(owner, "Team");
        await _service.AddParticipantAsync(chat.Id, owner, member);

        await Assert.ThrowsAsync<NotAllowedException>(() =>
            _service.AddParticipantAsync(chat.Id, member, Guid.NewGuid()));
    }

    [Fact]
    public async Task AddParticipant_Duplicate_ThrowsConflict()
    {
        var owner = Guid.NewGuid();
        var user = Guid.NewGuid();
        var chat = await _service.CreateGroupAsync(owner, "Team");
        await _service.AddParticipantAsync(chat.Id, owner, user);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.AddParticipantAsync(chat.Id, owner, user));
    }

    [Fact]
    public async Task Time_IsRoundedToMicroseconds()
    {
        var withSubMicroseconds = Now.AddTicks(1234567); // 123.4567 ms
        var service = CreateService(_messageClient, withSubMicroseconds);

        var chat = await service.CreateGroupAsync(Guid.NewGuid(), "Team");

        Assert.Equal(Now.AddTicks(1234560), chat.CreatedAt);
    }
}

using ChatService.Domain;
using ChatService.Infrastructure.Persistence;

namespace ChatService.Tests.Persistence;

[Trait("Category", "Integration")]
public class ChatRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly DapperChatRepository _repository = new(TestDatabase.DataSource);

    [Fact]
    public async Task AddAsync_ThenGetAsync_RoundTripsChatAndParticipants()
    {
        var owner = Guid.NewGuid();
        var chat = Chat.CreateGroup(owner, new ChatTitle("Team"), Now);

        await _repository.AddAsync(chat);
        var loaded = await _repository.GetAsync(chat.Id);

        Assert.NotNull(loaded);
        Assert.Equal(ChatType.Group, loaded.Type);
        Assert.Equal(new ChatTitle("Team"), loaded.Title);
        Assert.Equal(Now, loaded.CreatedAt);
        var participant = Assert.Single(loaded.Participants);
        Assert.Equal(owner, participant.UserId);
        Assert.Equal(ParticipantRole.Owner, participant.Role);
    }

    [Fact]
    public async Task AddAsync_DirectChat_HasNoTitle()
    {
        var chat = Chat.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Now);

        await _repository.AddAsync(chat);
        var loaded = await _repository.GetAsync(chat.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.Title);
        Assert.Equal(2, loaded.Participants.Count);
    }

    [Fact]
    public async Task AddParticipantAsync_IsPersisted()
    {
        var owner = Guid.NewGuid();
        var newUser = Guid.NewGuid();
        var chat = Chat.CreateGroup(owner, new ChatTitle("Team"), Now);
        await _repository.AddAsync(chat);

        var participant = chat.AddParticipant(owner, newUser, Now);
        await _repository.AddParticipantAsync(chat.Id, participant);
        var loaded = await _repository.GetAsync(chat.Id);

        Assert.NotNull(loaded);
        Assert.Contains(loaded.Participants, p => p.UserId == newUser && p.Role == ParticipantRole.Member);
    }

    [Fact]
    public async Task FindDirectAsync_FindsChatRegardlessOfOrder()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var chat = Chat.CreateDirect(userA, userB, Now);
        await _repository.AddAsync(chat);

        var found = await _repository.FindDirectAsync(userB, userA);

        Assert.Equal(chat.Id, found?.Id);
        Assert.Null(await _repository.FindDirectAsync(userA, Guid.NewGuid()));
    }

    [Fact]
    public async Task AddAsync_ConcurrentDirectDuplicate_ThrowsConflict()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        // To "requests" har begge fået null fra FindDirectAsync og opretter hver sin chat.
        await _repository.AddAsync(Chat.CreateDirect(userA, userB, Now));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _repository.AddAsync(Chat.CreateDirect(userB, userA, Now)));
    }

    [Fact]
    public async Task FindDirectAsync_IgnoresGroupChats()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var group = Chat.CreateGroup(owner, new ChatTitle("Team"), Now);
        await _repository.AddAsync(group);
        await _repository.AddParticipantAsync(group.Id, group.AddParticipant(owner, other, Now));

        Assert.Null(await _repository.FindDirectAsync(owner, other));
    }

    [Fact]
    public async Task AddParticipantAsync_ConcurrentDuplicate_ThrowsConflict()
    {
        var owner = Guid.NewGuid();
        var user = Guid.NewGuid();
        var chat = Chat.CreateGroup(owner, new ChatTitle("Team"), Now);
        await _repository.AddAsync(chat);

        // To "requests" indlæser chatten før nogen af dem har gemt - domænets dublet-tjek ser intet.
        var firstRequest = (await _repository.GetAsync(chat.Id))!;
        var secondRequest = (await _repository.GetAsync(chat.Id))!;
        await _repository.AddParticipantAsync(chat.Id, firstRequest.AddParticipant(owner, user, Now));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _repository.AddParticipantAsync(chat.Id, secondRequest.AddParticipant(owner, user, Now)));
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await _repository.GetAsync(Guid.NewGuid()));
    }
}

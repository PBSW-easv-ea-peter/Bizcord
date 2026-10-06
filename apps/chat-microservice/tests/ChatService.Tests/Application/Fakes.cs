using ChatService.Application;
using ChatService.Domain;

namespace ChatService.Tests.Application;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class InMemoryChatRepository : IChatRepository
{
    public Dictionary<Guid, Chat> Chats { get; } = [];
    public List<ChatParticipant> AddedParticipants { get; } = [];

    /// <summary>Simulerer en samtidig request: gemmes lige før AddAsync, som så giver en konflikt.</summary>
    public Chat? RaceWinner { get; set; }

    public Task<Chat?> GetAsync(Guid chatId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Chats.GetValueOrDefault(chatId));

    public Task<Chat?> FindDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default) =>
        Task.FromResult(Chats.Values.FirstOrDefault(c =>
            c.Type == ChatType.Direct &&
            c.Participants.Any(p => p.UserId == userA) &&
            c.Participants.Any(p => p.UserId == userB)));

    public Task AddAsync(Chat chat, CancellationToken cancellationToken = default)
    {
        if (RaceWinner is not null)
        {
            Chats.Add(RaceWinner.Id, RaceWinner);
            RaceWinner = null;
            throw new ConflictException("A direct chat between these users already exists.");
        }

        Chats.Add(chat.Id, chat);
        return Task.CompletedTask;
    }

    public Task AddParticipantAsync(Guid chatId, ChatParticipant participant, CancellationToken cancellationToken = default)
    {
        AddedParticipants.Add(participant);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryMessageRepository : IMessageRepository
{
    public Dictionary<Guid, Message> Messages { get; } = [];
    public List<Guid> GetCalls { get; } = [];
    public int? LastPageLimit { get; private set; }

    public Task<Message?> GetAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        GetCalls.Add(messageId);
        return Task.FromResult(Messages.GetValueOrDefault(messageId));
    }

    public Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message.Id, message);
        return Task.CompletedTask;
    }

    public List<Message> Updated { get; } = [];

    /// <summary>Simulerer en samtidig sletning: UpdateAsync gemmer intet og returnerer false.</summary>
    public bool LoseUpdateRace { get; set; }

    public Task<bool> UpdateAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (LoseUpdateRace)
            return Task.FromResult(false);

        Updated.Add(message);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Message>> GetPageAsync(Guid chatId, Message? before, int limit, CancellationToken cancellationToken = default)
    {
        LastPageLimit = limit;
        IReadOnlyList<Message> page = Messages.Values
            .Where(m => m.ChatId == chatId && (before is null || m.SentAt < before.SentAt))
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(page);
    }
}

internal sealed class InMemoryMessageClient : IMessageClient
{
    public List<object> Published { get; } = [];

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        Published.Add(message!);
        return Task.CompletedTask;
    }
}

internal sealed class FailingMessageClient : IMessageClient
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Broker is down.");
}

internal sealed class InMemoryReceiptRepository : IReceiptRepository
{
    public List<(Guid UserId, Message UpTo, DateTimeOffset Now)> MarkSeenCalls { get; } = [];

    /// <summary>Hvad MarkSeenUpToAsync returnerer (antal nyligt markerede).</summary>
    public int MarkedCount { get; set; }

    public Task<int> MarkSeenUpToAsync(Guid userId, Message upTo, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        MarkSeenCalls.Add((userId, upTo, now));
        return Task.FromResult(MarkedCount);
    }

    public List<(Guid MessageId, IReadOnlyCollection<Guid> UserIds, DateTimeOffset At)> MarkDeliveredCalls { get; } = [];

    public Task<int> MarkDeliveredAsync(Guid messageId, IReadOnlyCollection<Guid> userIds, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        MarkDeliveredCalls.Add((messageId, userIds, at));
        return Task.FromResult(MarkedCount);
    }

    public Task<IReadOnlyList<MessageReceipt>> GetForMessageAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MessageReceipt>>([]);
}

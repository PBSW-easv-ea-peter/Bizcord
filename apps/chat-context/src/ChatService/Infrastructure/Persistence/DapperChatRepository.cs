using ChatService.Application;
using ChatService.Domain;
using Dapper;
using Npgsql;

namespace ChatService.Infrastructure.Persistence;

public sealed class DapperChatRepository(NpgsqlDataSource dataSource) : IChatRepository
{
    private const string InsertParticipantSql = """
        insert into chat_participants (id, chat_id, user_id, role, joined_at, left_at)
        values (@Id, @ChatId, @UserId, @Role, @JoinedAt, @LeftAt)
        """;

    public async Task<Chat?> GetAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var chat = await connection.QuerySingleOrDefaultAsync<ChatRow>(new CommandDefinition(
            "select id, type, title, created_at as CreatedAt from chats where id = @ChatId",
            new { ChatId = chatId }, cancellationToken: cancellationToken));

        if (chat is null)
            return null;

        var participants = await connection.QueryAsync<ParticipantRow>(new CommandDefinition(
            """
            select id, user_id as UserId, role, joined_at as JoinedAt, left_at as LeftAt
            from chat_participants
            where chat_id = @ChatId
            """,
            new { ChatId = chatId }, cancellationToken: cancellationToken));

        return Chat.Restore(
            chat.Id,
            Enum.Parse<ChatType>(chat.Type),
            chat.Title is null ? null : new ChatTitle(chat.Title),
            DbTime.FromDb(chat.CreatedAt),
            participants.Select(ToDomain));
    }

    public async Task<Chat?> FindDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
    {
        Guid? chatId;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            chatId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "select id from chats where direct_key = @DirectKey",
                new { DirectKey = DirectKey(userA, userB) }, cancellationToken: cancellationToken));
        }

        return chatId is null ? null : await GetAsync(chatId.Value, cancellationToken);
    }

    /// <summary>Rækkefølge-uafhængig nøgle for et brugerpar - en persistensdetalje, ikke et domænebegreb.</summary>
    private static string DirectKey(Guid userA, Guid userB)
    {
        var (a, b) = (userA.ToString(), userB.ToString());
        return string.CompareOrdinal(a, b) <= 0 ? $"{a}:{b}" : $"{b}:{a}";
    }

    public async Task AddAsync(Chat chat, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into chats (id, type, title, created_at, direct_key)
                values (@Id, @Type, @Title, @CreatedAt, @DirectKey)
                """,
                new
                {
                    chat.Id,
                    Type = chat.Type.ToString(),
                    Title = chat.Title?.Value,
                    CreatedAt = DbTime.ToDb(chat.CreatedAt),
                    DirectKey = chat.Type == ChatType.Direct
                        ? DirectKey(chat.Participants[0].UserId, chat.Participants[1].UserId)
                        : null
                },
                transaction, cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (ex.ConstraintName == "uq_chats_direct_key")
        {
            // Samtidig oprettelse af samme direct-chat - den anden request nåede først.
            throw new ConflictException("A direct chat between these users already exists.");
        }

        foreach (var participant in chat.Participants)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertParticipantSql, ToParameters(chat.Id, participant), transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddParticipantAsync(Guid chatId, ChatParticipant participant, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                InsertParticipantSql, ToParameters(chatId, participant), cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Samtidig AddParticipant for samme bruger - domænets dublet-tjek så ikke den anden.
            throw new ConflictException("User is already a participant.");
        }
    }

    private static object ToParameters(Guid chatId, ChatParticipant participant) => new
    {
        participant.Id,
        ChatId = chatId,
        participant.UserId,
        Role = participant.Role.ToString(),
        JoinedAt = DbTime.ToDb(participant.JoinedAt),
        LeftAt = participant.LeftAt is null ? (DateTimeOffset?)null : DbTime.ToDb(participant.LeftAt.Value)
    };

    private static ChatParticipant ToDomain(ParticipantRow row) => new(
        row.Id,
        row.UserId,
        Enum.Parse<ParticipantRole>(row.Role),
        DbTime.FromDb(row.JoinedAt),
        DbTime.FromDb(row.LeftAt));

    private sealed class ChatRow
    {
        public Guid Id { get; init; }
        public string Type { get; init; } = "";
        public string? Title { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class ParticipantRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public string Role { get; init; } = "";
        public DateTime JoinedAt { get; init; }
        public DateTime? LeftAt { get; init; }
    }
}

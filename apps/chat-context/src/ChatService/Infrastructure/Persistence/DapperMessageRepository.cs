using ChatService.Application;
using ChatService.Domain;
using Dapper;
using Npgsql;

namespace ChatService.Infrastructure.Persistence;

public sealed class DapperMessageRepository(NpgsqlDataSource dataSource) : IMessageRepository
{
    private const string SelectColumns =
        "select id, chat_id as ChatId, sender_user_id as SenderUserId, content, sent_at as SentAt from messages";

    public async Task<Message?> GetAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MessageRow>(new CommandDefinition(
            $"{SelectColumns} where id = @MessageId",
            new { MessageId = messageId }, cancellationToken: cancellationToken));

        return row is null ? null : ToDomain(row);
    }

    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into messages (id, chat_id, sender_user_id, content, sent_at)
            values (@Id, @ChatId, @SenderUserId, @Content, @SentAt)
            """,
            new
            {
                message.Id,
                message.ChatId,
                message.SenderUserId,
                Content = message.Content.Value,
                SentAt = DbTime.ToDb(message.SentAt)
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Message>> GetPageAsync(Guid chatId, Message? before, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Keyset paging on (sent_at, id) - unambiguous order even when sent_at is equal.
        var sql = before is null
            ? $"{SelectColumns} where chat_id = @ChatId order by sent_at desc, id desc limit @Limit"
            : $"{SelectColumns} where chat_id = @ChatId and (sent_at, id) < (@BeforeSentAt, @BeforeId) order by sent_at desc, id desc limit @Limit";

        var rows = await connection.QueryAsync<MessageRow>(new CommandDefinition(
            sql,
            new
            {
                ChatId = chatId,
                Limit = limit,
                BeforeSentAt = before is null ? default : DbTime.ToDb(before.SentAt),
                BeforeId = before?.Id ?? Guid.Empty
            },
            cancellationToken: cancellationToken));

        return rows.Select(ToDomain).ToList();
    }

    private static Message ToDomain(MessageRow row) => Message.Restore(
        row.Id,
        row.ChatId,
        row.SenderUserId,
        new MessageContent(row.Content),
        DbTime.FromDb(row.SentAt));

    private sealed class MessageRow
    {
        public Guid Id { get; init; }
        public Guid ChatId { get; init; }
        public Guid SenderUserId { get; init; }
        public string Content { get; init; } = "";
        public DateTime SentAt { get; init; }
    }
}

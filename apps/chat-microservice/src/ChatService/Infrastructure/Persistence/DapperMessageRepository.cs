using ChatService.Application;
using ChatService.Domain;
using Dapper;
using Npgsql;

namespace ChatService.Infrastructure.Persistence;

public sealed class DapperMessageRepository(NpgsqlDataSource dataSource) : IMessageRepository
{
    private const string SelectColumns =
        "select id, chat_id as ChatId, sender_user_id as SenderUserId, content, sent_at as SentAt, " +
        "edited_at as EditedAt, deleted_at as DeletedAt, attachment_file_id as AttachmentFileId, " +
        "attachment_file_name as AttachmentFileName, attachment_size_bytes as AttachmentSizeBytes from messages";

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

        await InsertAsync(connection, null, message, cancellationToken);
    }

    /// <summary>Takes an optional transaction, so the coming saga repository can insert the message in the same transaction as its own update.</summary>
    internal static Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Message message, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            insert into messages (id, chat_id, sender_user_id, content, sent_at,
                                  attachment_file_id, attachment_file_name, attachment_size_bytes)
            values (@Id, @ChatId, @SenderUserId, @Content, @SentAt,
                    @AttachmentFileId, @AttachmentFileName, @AttachmentSizeBytes)
            """,
            new
            {
                message.Id,
                message.ChatId,
                message.SenderUserId,
                Content = message.Content?.Value,
                SentAt = DbTime.ToDb(message.SentAt),
                AttachmentFileId = message.Attachment?.FileId,
                AttachmentFileName = message.Attachment?.FileName,
                AttachmentSizeBytes = message.Attachment?.SizeBytes
            },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<bool> UpdateAsync(Message message, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var updated = await connection.ExecuteAsync(new CommandDefinition(
            """
            update messages
            set content = @Content, edited_at = @EditedAt, deleted_at = @DeletedAt,
                attachment_file_id = @AttachmentFileId, attachment_file_name = @AttachmentFileName,
                attachment_size_bytes = @AttachmentSizeBytes
            where id = @Id and deleted_at is null
            """,
            new
            {
                message.Id,
                Content = message.Content?.Value,
                AttachmentFileId = message.Attachment?.FileId,
                AttachmentFileName = message.Attachment?.FileName,
                AttachmentSizeBytes = message.Attachment?.SizeBytes,
                EditedAt = DbTime.ToDb(message.EditedAt),
                DeletedAt = DbTime.ToDb(message.DeletedAt)
            },
            cancellationToken: cancellationToken));

        return updated == 1;
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
        row.Content is null ? null : new MessageContent(row.Content),
        DbTime.FromDb(row.SentAt),
        DbTime.FromDb(row.EditedAt),
        DbTime.FromDb(row.DeletedAt),
        row.AttachmentFileId is { } fileId
            ? new Attachment(fileId, row.AttachmentFileName!, row.AttachmentSizeBytes!.Value)
            : null);

    private sealed class MessageRow
    {
        public Guid? AttachmentFileId { get; init; }
        public string? AttachmentFileName { get; init; }
        public long? AttachmentSizeBytes { get; init; }
        public Guid Id { get; init; }
        public Guid ChatId { get; init; }
        public Guid SenderUserId { get; init; }
        public string? Content { get; init; }
        public DateTime SentAt { get; init; }
        public DateTime? EditedAt { get; init; }
        public DateTime? DeletedAt { get; init; }
    }
}

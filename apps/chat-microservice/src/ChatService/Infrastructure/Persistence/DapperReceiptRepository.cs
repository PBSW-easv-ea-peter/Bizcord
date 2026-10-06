using ChatService.Application;
using ChatService.Domain;
using Dapper;
using Npgsql;

namespace ChatService.Infrastructure.Persistence;

public sealed class DapperReceiptRepository(NpgsqlDataSource dataSource) : IReceiptRepository
{
    public async Task<int> MarkSeenUpToAsync(Guid userId, Message upTo, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Set-baseret version af MessageReceipt.Create + MarkSeen:
        // ingen receipt på egne beskeder, og seen_at overskrives aldrig.
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into message_receipts (message_id, user_id, seen_at)
            select m.id, @UserId, @Now
            from messages m
            where m.chat_id = @ChatId
              and m.sender_user_id <> @UserId
              and (m.sent_at, m.id) <= (@UpToSentAt, @UpToId)
            on conflict (message_id, user_id)
            do update set seen_at = excluded.seen_at
            where message_receipts.seen_at is null
            """,
            new
            {
                UserId = userId,
                Now = DbTime.ToDb(now),
                upTo.ChatId,
                UpToSentAt = DbTime.ToDb(upTo.SentAt),
                UpToId = upTo.Id
            },
            cancellationToken: cancellationToken));
    }

    public async Task<int> MarkDeliveredAsync(Guid messageId, IReadOnlyCollection<Guid> userIds, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Set-baseret version af MessageReceipt.Create + MarkDelivered:
        // kun aktive deltagere i chatten (eventet er input udefra - stol ikke på listen), ingen receipt
        // for afsenderen, og delivered_at overskrives aldrig (seen_at røres ikke).
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into message_receipts (message_id, user_id, delivered_at)
            select m.id, u.user_id, @At
            from messages m
            cross join unnest(@UserIds) as u(user_id)
            join chat_participants p
              on p.chat_id = m.chat_id
             and p.user_id = u.user_id
             and p.left_at is null
            where m.id = @MessageId
              and m.sender_user_id <> u.user_id
            on conflict (message_id, user_id)
            do update set delivered_at = excluded.delivered_at
            where message_receipts.delivered_at is null
            """,
            new { MessageId = messageId, UserIds = userIds.ToArray(), At = DbTime.ToDb(at) },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MessageReceipt>> GetForMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ReceiptRow>(new CommandDefinition(
            """
            select message_id as MessageId, user_id as UserId, delivered_at as DeliveredAt, seen_at as SeenAt
            from message_receipts
            where message_id = @MessageId
            """,
            new { MessageId = messageId }, cancellationToken: cancellationToken));

        return rows
            .Select(r => MessageReceipt.Restore(r.MessageId, r.UserId, DbTime.FromDb(r.DeliveredAt), DbTime.FromDb(r.SeenAt)))
            .ToList();
    }

    private sealed class ReceiptRow
    {
        public Guid MessageId { get; init; }
        public Guid UserId { get; init; }
        public DateTime? DeliveredAt { get; init; }
        public DateTime? SeenAt { get; init; }
    }
}

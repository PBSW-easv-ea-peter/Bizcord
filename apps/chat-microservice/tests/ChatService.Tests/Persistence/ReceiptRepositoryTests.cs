using ChatService.Domain;
using ChatService.Infrastructure.Persistence;

namespace ChatService.Tests.Persistence;

[Trait("Category", "Integration")]
public class ReceiptRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly DapperChatRepository _chats = new(TestDatabase.DataSource);
    private readonly DapperMessageRepository _messages = new(TestDatabase.DataSource);
    private readonly DapperReceiptRepository _receipts = new(TestDatabase.DataSource);

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    private async Task<Message[]> CreateConversationAsync()
    {
        var chat = Chat.CreateDirect(_alice, _bob, Now);
        await _chats.AddAsync(chat);

        Message[] messages =
        [
            Message.Send(chat, _alice, new MessageContent("1"), Now),
            Message.Send(chat, _bob, new MessageContent("2"), Now.AddSeconds(1)),
            Message.Send(chat, _alice, new MessageContent("3"), Now.AddSeconds(2)),
            Message.Send(chat, _alice, new MessageContent("4"), Now.AddSeconds(3))
        ];
        foreach (var m in messages)
            await _messages.AddAsync(m);

        return messages;
    }

    [Fact]
    public async Task MarkSeenUpTo_MarksOthersMessagesUpToAndIncludingX()
    {
        var m = await CreateConversationAsync();

        var marked = await _receipts.MarkSeenUpToAsync(_bob, upTo: m[2], Now.AddMinutes(1));

        Assert.Equal(2, marked); // 1 and 3 - not Bob's own (2), not after X (4)
        Assert.Single(await _receipts.GetForMessageAsync(m[0].Id));
        Assert.Empty(await _receipts.GetForMessageAsync(m[1].Id));
        Assert.Single(await _receipts.GetForMessageAsync(m[2].Id));
        Assert.Empty(await _receipts.GetForMessageAsync(m[3].Id));
    }

    [Fact]
    public async Task MarkSeenUpTo_Again_KeepsFirstTimestamp()
    {
        var m = await CreateConversationAsync();
        await _receipts.MarkSeenUpToAsync(_bob, upTo: m[0], Now.AddMinutes(1));

        var marked = await _receipts.MarkSeenUpToAsync(_bob, upTo: m[3], Now.AddMinutes(5));

        Assert.Equal(2, marked); // only 3 and 4 are new
        var first = Assert.Single(await _receipts.GetForMessageAsync(m[0].Id));
        Assert.Equal(Now.AddMinutes(1), first.SeenAt);
        var last = Assert.Single(await _receipts.GetForMessageAsync(m[3].Id));
        Assert.Equal(Now.AddMinutes(5), last.SeenAt);
    }

    [Fact]
    public async Task MarkDelivered_SkipsSenderAndKeepsFirstTimestamp()
    {
        var m = await CreateConversationAsync();

        var marked = await _receipts.MarkDeliveredAsync(m[0].Id, [_bob, _alice], Now.AddMinutes(1)); // m[0] is Alice's
        var again = await _receipts.MarkDeliveredAsync(m[0].Id, [_bob], Now.AddMinutes(5));

        Assert.Equal(1, marked); // only Bob - no receipt for the sender
        Assert.Equal(0, again);
        var receipt = Assert.Single(await _receipts.GetForMessageAsync(m[0].Id));
        Assert.Equal(_bob, receipt.UserId);
        Assert.Equal(Now.AddMinutes(1), receipt.DeliveredAt);
        Assert.Null(receipt.SeenAt);
    }

    [Fact]
    public async Task MarkDelivered_AndSeen_DoNotOverwriteEachOther()
    {
        var m = await CreateConversationAsync();

        await _receipts.MarkDeliveredAsync(m[0].Id, [_bob], Now.AddMinutes(1));
        await _receipts.MarkSeenUpToAsync(_bob, upTo: m[0], Now.AddMinutes(2));

        var receipt = Assert.Single(await _receipts.GetForMessageAsync(m[0].Id));
        Assert.Equal(Now.AddMinutes(1), receipt.DeliveredAt);
        Assert.Equal(Now.AddMinutes(2), receipt.SeenAt);
    }

    [Fact]
    public async Task MarkDelivered_IgnoresUsersOutsideTheChat()
    {
        var m = await CreateConversationAsync();
        var outsider = Guid.NewGuid();

        var marked = await _receipts.MarkDeliveredAsync(m[0].Id, [_bob, outsider], Now.AddMinutes(1));

        Assert.Equal(1, marked);
        Assert.Equal(_bob, Assert.Single(await _receipts.GetForMessageAsync(m[0].Id)).UserId);
    }

    [Fact]
    public async Task MarkDelivered_UnknownMessage_MarksNothing()
    {
        Assert.Equal(0, await _receipts.MarkDeliveredAsync(Guid.NewGuid(), [_bob], Now));
    }
}

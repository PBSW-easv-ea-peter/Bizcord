using ChatService.Domain;
using ChatService.Infrastructure.Persistence;

namespace ChatService.Tests.Persistence;

[Trait("Category", "Integration")]
public class MessageRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly DapperChatRepository _chats = new(TestDatabase.DataSource);
    private readonly DapperMessageRepository _messages = new(TestDatabase.DataSource);

    [Fact]
    public async Task AddAsync_ThenGetAsync_RoundTripsMessage()
    {
        var sender = Guid.NewGuid();
        var chat = Chat.CreateDirect(sender, Guid.NewGuid(), Now);
        await _chats.AddAsync(chat);
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);

        await _messages.AddAsync(message);
        var loaded = await _messages.GetAsync(message.Id);

        Assert.NotNull(loaded);
        Assert.Equal(chat.Id, loaded.ChatId);
        Assert.Equal(sender, loaded.SenderUserId);
        Assert.Equal(new MessageContent("Hej"), loaded.Content);
        Assert.Equal(Now, loaded.SentAt);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsNewestFirstAndPagesBackwards()
    {
        var sender = Guid.NewGuid();
        var chat = Chat.CreateDirect(sender, Guid.NewGuid(), Now);
        await _chats.AddAsync(chat);
        var first = Message.Send(chat, sender, new MessageContent("1"), Now);
        var second = Message.Send(chat, sender, new MessageContent("2"), Now.AddSeconds(1));
        var third = Message.Send(chat, sender, new MessageContent("3"), Now.AddSeconds(2));
        foreach (var m in new[] { first, second, third })
            await _messages.AddAsync(m);

        var page1 = await _messages.GetPageAsync(chat.Id, before: null, limit: 2);
        var page2 = await _messages.GetPageAsync(chat.Id, before: page1[^1], limit: 2);

        Assert.Equal([third.Id, second.Id], page1.Select(m => m.Id));
        Assert.Equal([first.Id], page2.Select(m => m.Id));
    }

    [Fact]
    public async Task UpdateAsync_Edit_RoundTripsContentAndEditedAt()
    {
        var (chat, message) = await AddMessageAsync();
        message.Edit(chat, message.SenderUserId, new MessageContent("Hej igen"), Now.AddMinutes(1));

        var saved = await _messages.UpdateAsync(message);
        var loaded = await _messages.GetAsync(message.Id);

        Assert.True(saved);
        Assert.Equal(new MessageContent("Hej igen"), loaded!.Content);
        Assert.Equal(Now.AddMinutes(1), loaded.EditedAt);
        Assert.Null(loaded.DeletedAt);
    }

    [Fact]
    public async Task UpdateAsync_Delete_RemovesContentAndKeepsMessageInPage()
    {
        var (chat, message) = await AddMessageAsync();
        message.Delete(chat, message.SenderUserId, Now.AddMinutes(1));

        await _messages.UpdateAsync(message);
        var loaded = await _messages.GetAsync(message.Id);
        var page = await _messages.GetPageAsync(chat.Id, before: null, limit: 10);

        Assert.Null(loaded!.Content);
        Assert.Equal(Now.AddMinutes(1), loaded.DeletedAt);
        Assert.Equal(message.Id, Assert.Single(page).Id);
    }

    [Fact]
    public async Task UpdateAsync_AlreadyDeletedInDb_ReturnsFalseAndKeepsDeletion()
    {
        var (chat, message) = await AddMessageAsync();
        // To kopier af samme besked - som to samtidige requests.
        var deleting = (await _messages.GetAsync(message.Id))!;
        var editing = (await _messages.GetAsync(message.Id))!;
        deleting.Delete(chat, message.SenderUserId, Now);
        await _messages.UpdateAsync(deleting);

        editing.Edit(chat, message.SenderUserId, new MessageContent("For sent"), Now);
        var saved = await _messages.UpdateAsync(editing);

        Assert.False(saved);
        Assert.Null((await _messages.GetAsync(message.Id))!.Content);
    }

    private async Task<(Chat Chat, Message Message)> AddMessageAsync()
    {
        var sender = Guid.NewGuid();
        var chat = Chat.CreateDirect(sender, Guid.NewGuid(), Now);
        await _chats.AddAsync(chat);
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);
        await _messages.AddAsync(message);
        return (chat, message);
    }
}

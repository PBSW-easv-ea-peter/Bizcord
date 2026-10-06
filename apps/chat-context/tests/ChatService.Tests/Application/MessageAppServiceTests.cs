using ChatService.Application;
using ChatService.Contracts;
using ChatService.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChatService.Tests.Application;

public class MessageAppServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryChatRepository _chats = new();
    private readonly InMemoryMessageRepository _messages = new();
    private readonly InMemoryReceiptRepository _receipts = new();
    private readonly InMemoryMessageClient _messageClient = new();
    private readonly MessageAppService _service;

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Chat _chat;

    public MessageAppServiceTests()
    {
        _service = CreateService(_messageClient);
        _chat = Chat.CreateDirect(_alice, _bob, Now);
        _chats.Chats.Add(_chat.Id, _chat);
    }

    private MessageAppService CreateService(IMessageClient messageClient) =>
        new(_chats, _messages, _receipts, messageClient, new FixedTimeProvider(Now), NullLogger<MessageAppService>.Instance);

    [Fact]
    public async Task Send_PublishesMessageSentWithRecipientsExcludingSender()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");

        var published = Assert.IsType<MessageSent>(Assert.Single(_messageClient.Published));
        Assert.Equal(message.Id, published.MessageId);
        Assert.Equal("Hej", published.Content);
        Assert.Equal([_bob], published.RecipientUserIds);
    }

    [Fact]
    public async Task Send_WhenBrokerFails_StillSavesAndReturns()
    {
        var service = CreateService(new FailingMessageClient());

        var message = await service.SendAsync(_chat.Id, _alice, "Hej");

        Assert.Same(message, _messages.Messages[message.Id]);
    }

    [Fact]
    public async Task MarkSeenUpTo_WhenSomethingMarked_PublishesMessagesSeen()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");
        _messageClient.Published.Clear();
        _receipts.MarkedCount = 1;

        await _service.MarkSeenUpToAsync(_chat.Id, _bob, message.Id);

        var published = Assert.IsType<MessagesSeen>(Assert.Single(_messageClient.Published));
        Assert.Equal(new MessagesSeen(_chat.Id, _bob, message.Id, Now), published);
    }

    [Fact]
    public async Task MarkSeenUpTo_WhenNothingNew_PublishesNothing()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");
        _messageClient.Published.Clear();
        _receipts.MarkedCount = 0;

        await _service.MarkSeenUpToAsync(_chat.Id, _bob, message.Id);

        Assert.Empty(_messageClient.Published);
    }

    [Fact]
    public async Task Send_ByParticipant_IsSaved()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");

        Assert.Same(message, _messages.Messages[message.Id]);
        Assert.Equal(Now, message.SentAt);
    }

    [Fact]
    public async Task Send_UnknownChat_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SendAsync(Guid.NewGuid(), _alice, "Hej"));
    }

    [Fact]
    public async Task Send_ByNonParticipant_ThrowsNotAllowed()
    {
        await Assert.ThrowsAsync<NotAllowedException>(() => _service.SendAsync(_chat.Id, Guid.NewGuid(), "Hej"));
    }

    [Fact]
    public async Task GetPage_ByNonParticipant_ThrowsNotAllowed()
    {
        await Assert.ThrowsAsync<NotAllowedException>(() =>
            _service.GetPageAsync(_chat.Id, Guid.NewGuid(), beforeMessageId: null));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1000, MessageAppService.MaxPageSize)]
    [InlineData(20, 20)]
    public async Task GetPage_LimitIsClamped(int requested, int expected)
    {
        await _service.GetPageAsync(_chat.Id, _alice, beforeMessageId: null, requested);

        Assert.Equal(expected, _messages.LastPageLimit);
    }

    [Fact]
    public async Task GetPage_BeforeMessageFromOtherChat_ThrowsNotFound()
    {
        var otherMessage = await SendInOtherChatAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetPageAsync(_chat.Id, _alice, beforeMessageId: otherMessage.Id));
    }

    [Fact]
    public async Task MarkSeenUpTo_LoadsCursorFromRepository()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");

        await _service.MarkSeenUpToAsync(_chat.Id, _bob, message.Id);

        Assert.Contains(message.Id, _messages.GetCalls);
        var call = Assert.Single(_receipts.MarkSeenCalls);
        Assert.Equal(_bob, call.UserId);
        Assert.Same(_messages.Messages[message.Id], call.UpTo);
    }

    [Fact]
    public async Task MarkSeenUpTo_MessageFromOtherChat_ThrowsNotFound()
    {
        var otherMessage = await SendInOtherChatAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.MarkSeenUpToAsync(_chat.Id, _bob, otherMessage.Id));
        Assert.Empty(_receipts.MarkSeenCalls);
    }

    [Fact]
    public async Task MarkDelivered_PassesToRepository()
    {
        var messageId = Guid.NewGuid();

        await _service.MarkDeliveredAsync(messageId, [_bob], Now);

        var call = Assert.Single(_receipts.MarkDeliveredCalls);
        Assert.Equal(messageId, call.MessageId);
        Assert.Equal([_bob], call.UserIds);
        Assert.Equal(Now, call.At);
    }

    [Fact]
    public async Task MarkDelivered_UnknownMessage_DoesNotThrow()
    {
        _receipts.MarkedCount = 0; // som repository'et svarer for en ukendt besked

        await _service.MarkDeliveredAsync(Guid.NewGuid(), [_bob], Now);
    }

    [Fact]
    public async Task GetReceipts_ByNonParticipant_ThrowsNotAllowed()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");

        await Assert.ThrowsAsync<NotAllowedException>(() =>
            _service.GetReceiptsAsync(_chat.Id, Guid.NewGuid(), message.Id));
    }

    [Fact]
    public async Task Edit_BySender_SavesAndPublishesMessageEdited()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");
        _messageClient.Published.Clear();

        var edited = await _service.EditAsync(_chat.Id, _alice, message.Id, "Hej igen");

        Assert.Same(edited, Assert.Single(_messages.Updated));
        var published = Assert.IsType<MessageEdited>(Assert.Single(_messageClient.Published));
        Assert.Equal(message.Id, published.MessageId);
        Assert.Equal("Hej igen", published.Content);
        Assert.Equal(Now, published.EditedAt);
        Assert.Equal([_bob], published.RecipientUserIds);
    }

    [Fact]
    public async Task Edit_ByOtherParticipant_ThrowsNotAllowedAndSavesNothing()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");

        await Assert.ThrowsAsync<NotAllowedException>(() => _service.EditAsync(_chat.Id, _bob, message.Id, "Hacket"));
        Assert.Empty(_messages.Updated);
    }

    [Fact]
    public async Task Edit_MessageFromOtherChat_ThrowsNotFound()
    {
        var otherMessage = await SendInOtherChatAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.EditAsync(_chat.Id, _alice, otherMessage.Id, "Hej"));
    }

    [Fact]
    public async Task Edit_WhenDeletedConcurrently_ThrowsConflictAndPublishesNothing()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");
        _messageClient.Published.Clear();
        _messages.LoseUpdateRace = true;

        await Assert.ThrowsAsync<ConflictException>(() => _service.EditAsync(_chat.Id, _alice, message.Id, "Hej igen"));
        Assert.Empty(_messageClient.Published);
    }

    [Fact]
    public async Task Delete_BySender_SavesAndPublishesMessageDeleted()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");
        _messageClient.Published.Clear();

        await _service.DeleteAsync(_chat.Id, _alice, message.Id);

        Assert.True(Assert.Single(_messages.Updated).IsDeleted);
        var published = Assert.IsType<MessageDeleted>(Assert.Single(_messageClient.Published));
        Assert.Equal(new MessageDeleted(message.Id, _chat.Id, Now, published.RecipientUserIds), published);
        Assert.Equal([_bob], published.RecipientUserIds);
    }

    [Fact]
    public async Task Delete_Twice_PublishesOnlyOnce()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");
        _messageClient.Published.Clear();

        await _service.DeleteAsync(_chat.Id, _alice, message.Id);
        await _service.DeleteAsync(_chat.Id, _alice, message.Id);

        Assert.Single(_messages.Updated);
        Assert.IsType<MessageDeleted>(Assert.Single(_messageClient.Published));
    }

    [Fact]
    public async Task Delete_ByOtherParticipant_ThrowsNotAllowed()
    {
        var message = await _service.SendAsync(_chat.Id, _alice, "Hej");

        await Assert.ThrowsAsync<NotAllowedException>(() => _service.DeleteAsync(_chat.Id, _bob, message.Id));
        Assert.Empty(_messages.Updated);
    }

    private async Task<Message> SendInOtherChatAsync()
    {
        var other = Chat.CreateDirect(_alice, Guid.NewGuid(), Now);
        _chats.Chats.Add(other.Id, other);
        return await _service.SendAsync(other.Id, _alice, "Andet sted");
    }
}

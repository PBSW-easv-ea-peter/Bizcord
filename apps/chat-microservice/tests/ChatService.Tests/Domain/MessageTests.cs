using ChatService.Domain;

namespace ChatService.Tests.Domain;

public class MessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Send_ByParticipant_CreatesMessage()
    {
        var sender = Guid.NewGuid();
        var chat = Chat.CreateDirect(sender, Guid.NewGuid(), Now);

        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);

        Assert.Equal(chat.Id, message.ChatId);
        Assert.Equal(sender, message.SenderUserId);
        Assert.Equal("Hej", message.Content?.Value);
        Assert.Equal(Now, message.SentAt);
        Assert.Null(message.EditedAt);
        Assert.Null(message.DeletedAt);
    }

    [Fact]
    public void Send_ByNonParticipant_Throws()
    {
        var chat = Chat.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Throws<NotAllowedException>(() => Message.Send(chat, Guid.NewGuid(), new MessageContent("Hej"), Now));
    }

    [Fact]
    public void Edit_BySender_ReplacesContentAndSetsEditedAt()
    {
        var (chat, sender, _) = CreateChat();
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);

        message.Edit(chat, sender, new MessageContent("Hej igen"), Now.AddMinutes(1));

        Assert.Equal(new MessageContent("Hej igen"), message.Content);
        Assert.Equal(Now.AddMinutes(1), message.EditedAt);
        Assert.Equal(Now, message.SentAt);
    }

    [Fact]
    public void Edit_ByOtherParticipant_Throws()
    {
        var (chat, sender, other) = CreateChat();
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);

        Assert.Throws<NotAllowedException>(() => message.Edit(chat, other, new MessageContent("Hacket"), Now));
        Assert.Equal(new MessageContent("Hej"), message.Content);
    }

    [Fact]
    public void Edit_DeletedMessage_ThrowsConflict()
    {
        var (chat, sender, _) = CreateChat();
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);
        message.Delete(chat, sender, Now);

        Assert.Throws<ConflictException>(() => message.Edit(chat, sender, new MessageContent("Hej igen"), Now));
    }

    [Fact]
    public void Edit_WithOtherChat_Throws()
    {
        var (chat, sender, _) = CreateChat();
        var otherChat = Chat.CreateDirect(sender, Guid.NewGuid(), Now);
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);

        Assert.Throws<DomainException>(() => message.Edit(otherChat, sender, new MessageContent("Hej igen"), Now));
    }

    [Fact]
    public void Delete_BySender_RemovesContentAndSetsDeletedAt()
    {
        var (chat, sender, _) = CreateChat();
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);

        var deleted = message.Delete(chat, sender, Now.AddMinutes(1));

        Assert.True(deleted);
        Assert.True(message.IsDeleted);
        Assert.Null(message.Content);
        Assert.Equal(Now.AddMinutes(1), message.DeletedAt);
    }

    [Fact]
    public void Delete_Twice_IsIdempotentAndKeepsFirstDeletedAt()
    {
        var (chat, sender, _) = CreateChat();
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);
        message.Delete(chat, sender, Now);

        var deletedAgain = message.Delete(chat, sender, Now.AddMinutes(1));

        Assert.False(deletedAgain);
        Assert.Equal(Now, message.DeletedAt);
    }

    [Fact]
    public void Delete_ByOtherParticipant_Throws()
    {
        var (chat, sender, other) = CreateChat();
        var message = Message.Send(chat, sender, new MessageContent("Hej"), Now);

        Assert.Throws<NotAllowedException>(() => message.Delete(chat, other, Now));
        Assert.False(message.IsDeleted);
    }

    private static (Chat Chat, Guid Sender, Guid Other) CreateChat()
    {
        var sender = Guid.NewGuid();
        var other = Guid.NewGuid();
        return (Chat.CreateDirect(sender, other, Now), sender, other);
    }
}

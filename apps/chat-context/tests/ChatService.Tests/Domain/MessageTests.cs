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
        Assert.Equal("Hej", message.Content.Value);
        Assert.Equal(Now, message.SentAt);
    }

    [Fact]
    public void Send_ByNonParticipant_Throws()
    {
        var chat = Chat.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Throws<NotAllowedException>(() => Message.Send(chat, Guid.NewGuid(), new MessageContent("Hej"), Now));
    }
}

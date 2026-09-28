using ChatService.Domain;

namespace ChatService.Tests.Domain;

public class MessageReceiptTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _sender = Guid.NewGuid();
    private readonly Guid _recipient = Guid.NewGuid();
    private readonly Message _message;

    public MessageReceiptTests()
    {
        var chat = Chat.CreateDirect(_sender, _recipient, Now);
        _message = Message.Send(chat, _sender, new MessageContent("Hej"), Now);
    }

    [Fact]
    public void Create_ForSender_Throws()
    {
        Assert.Throws<DomainException>(() => MessageReceipt.Create(_message, _sender));
    }

    [Fact]
    public void MarkSeen_Twice_KeepsFirstTimestamp()
    {
        var receipt = MessageReceipt.Create(_message, _recipient);

        receipt.MarkSeen(Now.AddMinutes(1));
        receipt.MarkSeen(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(1), receipt.SeenAt);
    }

    [Fact]
    public void MarkDelivered_Twice_KeepsFirstTimestamp()
    {
        var receipt = MessageReceipt.Create(_message, _recipient);

        receipt.MarkDelivered(Now.AddMinutes(1));
        receipt.MarkDelivered(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(1), receipt.DeliveredAt);
    }
}

using ChatService.Domain;

namespace ChatService.Tests.Domain;

public class MessageAttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Chat _chat;
    private readonly Attachment _attachment = new(Guid.NewGuid(), "report.pdf", 2_000_000);

    public MessageAttachmentTests() => _chat = Chat.CreateDirect(_alice, Guid.NewGuid(), Now);

    [Fact]
    public void SendWithAttachment_WithoutText_IsAllowed()
    {
        var message = Message.SendWithAttachment(_chat, _alice, null, _attachment, Now);

        Assert.Null(message.Content);
        Assert.Equal(_attachment, message.Attachment);
    }

    [Fact]
    public void SendWithAttachment_ByNonParticipant_IsNotAllowed() =>
        Assert.Throws<NotAllowedException>(() => Message.SendWithAttachment(_chat, Guid.NewGuid(), null, _attachment, Now));

    [Fact]
    public void Delete_RemovesTheAttachmentToo()
    {
        var message = Message.SendWithAttachment(_chat, _alice, new MessageContent("Report"), _attachment, Now);

        message.Delete(_chat, _alice, Now);

        Assert.Null(message.Content);
        Assert.Null(message.Attachment);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Attachment_WithoutFileName_IsInvalid(string fileName) =>
        Assert.Throws<DomainException>(() => new Attachment(Guid.NewGuid(), fileName, 1));

    [Fact]
    public void Attachment_WithZeroBytes_IsInvalid() =>
        Assert.Throws<DomainException>(() => new Attachment(Guid.NewGuid(), "empty.txt", 0));
}

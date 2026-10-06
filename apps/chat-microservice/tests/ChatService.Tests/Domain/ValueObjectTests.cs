using ChatService.Domain;

namespace ChatService.Tests.Domain;

public class ValueObjectTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChatTitle_Empty_Throws(string value)
    {
        Assert.Throws<DomainException>(() => new ChatTitle(value));
    }

    [Fact]
    public void ChatTitle_TooLong_Throws()
    {
        Assert.Throws<DomainException>(() => new ChatTitle(new string('a', ChatTitle.MaxLength + 1)));
    }

    [Fact]
    public void ChatTitle_IsTrimmed()
    {
        Assert.Equal("Team", new ChatTitle("  Team  ").Value);
    }

    [Fact]
    public void ChatTitle_EqualByValue()
    {
        Assert.Equal(new ChatTitle("Team"), new ChatTitle("Team"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MessageContent_Empty_Throws(string value)
    {
        Assert.Throws<DomainException>(() => new MessageContent(value));
    }

    [Fact]
    public void MessageContent_TooLong_Throws()
    {
        Assert.Throws<DomainException>(() => new MessageContent(new string('a', MessageContent.MaxLength + 1)));
    }
}

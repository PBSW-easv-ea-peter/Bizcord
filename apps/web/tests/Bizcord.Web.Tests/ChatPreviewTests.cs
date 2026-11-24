using Bizcord.Models;
using Bizcord.Services;

namespace Bizcord.Web.Tests;

public class ChatPreviewTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static ChatDto Chat(string type, string? title) =>
        new(Guid.NewGuid(), type, title, Now, [new(Alice, "Member", null), new(Bob, "Member", null)]);

    [Fact]
    public void A_direct_chat_is_named_after_the_other_participant()
    {
        Assert.Equal("Bob", ChatPreview.NameOf(Chat("Direct", null), Alice));
        Assert.Equal("Alice", ChatPreview.NameOf(Chat("Direct", null), Bob));
    }

    [Fact]
    public void A_group_chat_is_named_by_its_title()
    {
        Assert.Equal("Project Team", ChatPreview.NameOf(Chat("Group", "Project Team"), Alice));
    }

    [Fact]
    public void The_preview_shows_a_file_name_when_a_message_only_has_a_file()
    {
        var message = new MessageDto(Guid.NewGuid(), Bob, null, Now, null, null, new AttachmentDto(Guid.NewGuid(), "video.mp4", 15_000_000));

        var preview = ChatPreview.From(Chat("Direct", null), message, Alice);

        Assert.Equal("Bob: 📎 video.mp4", preview.LastMessage);
        Assert.Equal(Now, preview.LastActivity);
    }

    [Fact]
    public void A_deleted_last_message_hides_its_content()
    {
        var message = new MessageDto(Guid.NewGuid(), Bob, null, Now, null, Now, null);

        var preview = ChatPreview.From(Chat("Direct", null), message, Alice);

        Assert.Equal("Bob: 🗑️ Beskeden er slettet", preview.LastMessage);
    }
}

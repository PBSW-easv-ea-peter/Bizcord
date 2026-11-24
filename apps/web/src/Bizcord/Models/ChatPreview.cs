using System.Globalization;
using Bizcord.Services;

namespace Bizcord.Models;

/// <summary>One line in the chat list: name, latest message, time and unread count.</summary>
/// <remarks>Unread is always 0 until the frontend reads receipts.</remarks>
public record ChatPreview(Guid Id, string Name, string LastMessage, string Time, int Unread, bool IsGroup, DateTimeOffset LastActivity)
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    public static ChatPreview From(ChatDto chat, MessageDto? last, Guid currentUserId) => new(
        chat.Id,
        NameOf(chat, currentUserId),
        LastMessageText(last),
        FormatTime(last?.SentAt),
        Unread: 0,
        chat.IsGroup,
        last?.SentAt ?? chat.CreatedAt);

    /// <summary>A group has a title; a direct chat is named after the other participant.</summary>
    public static string NameOf(ChatDto chat, Guid currentUserId)
    {
        if (!string.IsNullOrWhiteSpace(chat.Title))
        {
            return chat.Title;
        }

        var others = chat.Participants.Where(p => p.UserId != currentUserId).Select(p => TestUsers.NameOf(p.UserId)).ToList();
        return others.Count == 0 ? "Kun dig" : string.Join(", ", others);
    }

    private static string LastMessageText(MessageDto? last)
    {
        if (last is null)
        {
            return "Ingen beskeder endnu";
        }

        var sender = TestUsers.NameOf(last.SenderUserId);
        if (last.IsDeleted)
        {
            return $"{sender}: 🗑️ Beskeden er slettet";
        }

        return last.Content is null && last.Attachment is not null
            ? $"{sender}: 📎 {last.Attachment.FileName}"
            : $"{sender}: {last.Content}";
    }

    // Today: "10:24", yesterday: "I går", otherwise: "21. okt."
    private static string FormatTime(DateTimeOffset? sentAt)
    {
        if (sentAt is null)
        {
            return string.Empty;
        }

        var local = sentAt.Value.ToLocalTime();
        var today = DateTimeOffset.Now.Date;

        if (local.Date == today) return local.ToString("HH:mm", Danish);
        if (local.Date == today.AddDays(-1)) return "I går";
        return local.ToString("d. MMM", Danish);
    }
}

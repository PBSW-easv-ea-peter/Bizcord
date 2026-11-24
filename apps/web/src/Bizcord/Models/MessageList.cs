using System.Globalization;
using Bizcord.Services;

namespace Bizcord.Models;

/// <summary>
/// What the _ChatMessages partial needs: one page of messages (oldest first), whether older ones exist,
/// and who "me" is (own messages are shown on the right and can be edited/deleted).
/// </summary>
public record MessageList(IReadOnlyList<MessageDto> Messages, bool HasOlder, Guid CurrentUserId, Guid ChatId)
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    // Today: "10:24", otherwise: "21. okt. 10:24"
    public static string FormatTime(DateTimeOffset sentAt)
    {
        var local = sentAt.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date
            ? local.ToString("HH:mm", Danish)
            : local.ToString("d. MMM HH:mm", Danish);
    }

    // "15 MB", "820 KB"
    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1_000_000 => $"{bytes / 1_000_000.0:0.#} MB",
        >= 1_000 => $"{bytes / 1_000.0:0} KB",
        _ => $"{bytes} B"
    };
}

namespace RealTimeCommunicationServer.Contracts;

// RTC's own contracts towards the outside world. See docs/contracts.md.

/// <summary>
/// Published as 'rtc.message-delivered' when the message has been pushed to at least one connected recipient.
/// Only those who actually received it - offline recipients are not included.
/// </summary>
public record MessageDelivered(
    Guid MessageId,
    Guid ChatId,
    IReadOnlyList<Guid> DeliveredToUserIds,
    DateTimeOffset DeliveredAt);

/// <summary>Pushed to the client via SignalR as 'MessageReceived'.</summary>
public record MessageReceived(
    Guid MessageId,
    Guid ChatId,
    Guid SenderUserId,
    string Content,
    DateTimeOffset SentAt);

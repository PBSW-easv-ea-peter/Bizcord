namespace RealTimeCommunicationServer.Contracts;

// RTC's egne kontrakter mod omverdenen. Se docs/contracts.md.

/// <summary>
/// Publiceres som 'rtc.message-delivered', når beskeden er pushet til mindst én forbundet modtager.
/// Kun dem, der faktisk fik den - offline modtagere er ikke med.
/// </summary>
public record MessageDelivered(
    Guid MessageId,
    Guid ChatId,
    IReadOnlyList<Guid> DeliveredToUserIds,
    DateTimeOffset DeliveredAt);

/// <summary>Pushes til klienten via SignalR som 'MessageReceived'.</summary>
public record MessageReceived(
    Guid MessageId,
    Guid ChatId,
    Guid SenderUserId,
    string Content,
    DateTimeOffset SentAt);

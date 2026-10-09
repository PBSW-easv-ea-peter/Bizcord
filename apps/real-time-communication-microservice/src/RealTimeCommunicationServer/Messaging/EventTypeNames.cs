using EasyNetQ;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Messaging;

/// <summary>
/// Maps ChatService's logical event names to RTC's own types.
/// EasyNetQ derives the exchange name from this, so the names must match the publisher's exactly.
/// Unknown types (e.g. PingMessage) use EasyNetQ's default name.
/// </summary>
public class EventTypeNames : ITypeNameSerializer
{
    private readonly Dictionary<Type, string> _names = new()
    {
        [typeof(MessageSent)] = "chat.message-sent",
        [typeof(ParticipantAdded)] = "chat.participant-added",
        [typeof(MessagesSeen)] = "chat.messages-seen",
        [typeof(MessageDelivered)] = "rtc.message-delivered"
    };

    private readonly Dictionary<string, Type> _types;
    private readonly DefaultTypeNameSerializer _fallback = new();

    public EventTypeNames()
    {
        _types = _names.ToDictionary(pair => pair.Value, pair => pair.Key);
    }

    public string Serialize(Type type) =>
        _names.TryGetValue(type, out var name) ? name : _fallback.Serialize(type);

    public Type Deserialize(string typeName) =>
        _types.TryGetValue(typeName, out var type) ? type : _fallback.Deserialize(typeName);
}

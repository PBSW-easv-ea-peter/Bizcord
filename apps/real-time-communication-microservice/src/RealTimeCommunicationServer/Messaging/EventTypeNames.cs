using EasyNetQ;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Messaging;

/// <summary>
/// Oversætter ChatService's logiske event-navne til RTC's egne typer.
/// EasyNetQ udleder exchange-navnet herfra, så navnene skal matche publisherens præcist.
/// Ukendte typer (fx PingMessage) bruger EasyNetQ's standardnavn.
/// </summary>
public class EventTypeNames : ITypeNameSerializer
{
    private readonly Dictionary<Type, string> _names = new()
    {
        [typeof(MessageSent)] = "chat.message-sent",
        [typeof(ParticipantAdded)] = "chat.participant-added",
        [typeof(MessagesSeen)] = "chat.messages-seen"
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

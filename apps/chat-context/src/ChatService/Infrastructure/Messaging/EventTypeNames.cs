using ChatService.Contracts;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Replaces EasyNetQ's default ("Namespace.Type, Assembly") with logical event names.
/// EasyNetQ derives the exchange name and 'type' header from this, and the consumer looks up its own
/// C# type from the header - so publisher and consumer need not share an assembly or language.
/// </summary>
public sealed class EventTypeNames : ITypeNameSerializer
{
    public static readonly EventTypeNames ForChatService = new(new Dictionary<Type, string>
    {
        [typeof(MessageSent)] = EventNames.MessageSent,
        [typeof(ParticipantAdded)] = EventNames.ParticipantAdded,
        [typeof(MessagesSeen)] = EventNames.MessagesSeen,
        [typeof(MessageDelivered)] = EventNames.MessageDelivered
    });

    private readonly IReadOnlyDictionary<Type, string> _names;
    private readonly IReadOnlyDictionary<string, Type> _types;
    private readonly DefaultTypeNameSerializer _fallback = new();

    public EventTypeNames(IReadOnlyDictionary<Type, string> names)
    {
        _names = names;
        _types = names.ToDictionary(pair => pair.Value, pair => pair.Key);
    }

    // Unknown types (e.g. EasyNetQ's own) fall back to the default.
    public string Serialize(Type type) =>
        _names.TryGetValue(type, out var name) ? name : _fallback.Serialize(type);

    public Type Deserialize(string typeName) =>
        _types.TryGetValue(typeName, out var type) ? type : _fallback.Deserialize(typeName);
}

using ChatService.Contracts;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Erstatter EasyNetQ's standard ("Namespace.Type, Assembly") med logiske event-navne.
/// EasyNetQ udleder exchange-navn og 'type'-header herfra, og consumeren slår sin egen
/// C#-type op ud fra headeren - så publisher og consumer behøver ikke dele assembly eller sprog.
/// </summary>
public sealed class EventTypeNames : ITypeNameSerializer
{
    public static readonly EventTypeNames ForChatService = new(new Dictionary<Type, string>
    {
        [typeof(MessageSent)] = EventNames.MessageSent,
        [typeof(ParticipantAdded)] = EventNames.ParticipantAdded,
        [typeof(MessagesSeen)] = EventNames.MessagesSeen
    });

    private readonly IReadOnlyDictionary<Type, string> _names;
    private readonly IReadOnlyDictionary<string, Type> _types;
    private readonly DefaultTypeNameSerializer _fallback = new();

    public EventTypeNames(IReadOnlyDictionary<Type, string> names)
    {
        _names = names;
        _types = names.ToDictionary(pair => pair.Value, pair => pair.Key);
    }

    // Ukendte typer (fx EasyNetQ's egne) falder tilbage til standarden.
    public string Serialize(Type type) =>
        _names.TryGetValue(type, out var name) ? name : _fallback.Serialize(type);

    public Type Deserialize(string typeName) =>
        _types.TryGetValue(typeName, out var type) ? type : _fallback.Deserialize(typeName);
}

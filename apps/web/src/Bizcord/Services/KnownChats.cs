using System.Collections.Concurrent;

namespace Bizcord.Services;

/// <summary>
/// TEMPORARY: the chat ids this frontend knows about. ChatService has no "my chats" endpoint (GET /chats) yet,
/// so the chat list asks for each known chat and shows the ones the user is an active participant in.
/// Starts with the seed chats (apps/chat-microservice/chatDB/postgres/init/003_Seed_data.sql); chats created here are added.
/// In memory only - chats created through the frontend drop out of the list when it restarts (they still exist).
/// Remove when GET /chats exists.
/// </summary>
public sealed class KnownChats
{
    public static readonly IReadOnlyList<Guid> SeedChatIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111111"), // Alice <-> Bob
        Guid.Parse("22222222-2222-2222-2222-222222222222"), // Alice <-> Charlie
        Guid.Parse("33333333-3333-3333-3333-333333333333"), // Project Team
        Guid.Parse("44444444-4444-4444-4444-444444444444")  // Weekend Plans
    ];

    private readonly ConcurrentDictionary<Guid, byte> _ids = new(SeedChatIds.Select(id => KeyValuePair.Create(id, (byte)0)));

    public IReadOnlyCollection<Guid> All => _ids.Keys.ToList();

    public void Add(Guid chatId) => _ids.TryAdd(chatId, 0);
}

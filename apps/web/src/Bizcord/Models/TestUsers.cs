namespace Bizcord.Models;

public record TestUser(Guid Id, string Name);

/// <summary>
/// Hard-coded test users, because there is no UserService yet.
/// Alice, Bob, Charlie and Dave match the seed data (apps/chat-microservice/chatDB/postgres/init/003_Seed_data.sql),
/// so they have chats from the start. The others have none until someone starts a chat with them.
/// </summary>
public static class TestUsers
{
    /// <summary>Deliberately not in the seed data, so the error handling (e.g. 403) can be tried.</summary>
    public static readonly TestUser UnknownUser =
        new(Guid.Parse("99999999-9999-9999-9999-999999999999"), "Ukendt bruger (test af fejl)");

    /// <summary>Users you can log in as in the dropdown. The first one is the default.</summary>
    public static readonly IReadOnlyList<TestUser> CanLogIn =
    [
        new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Alice"),
        new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Bob"),
        new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Charlie"),
        new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Dave"),
        User(1, "Emma"), User(2, "Oliver"), User(3, "Noah"), User(4, "Sofie"), User(5, "Lucas"), User(6, "Freja"),
    ];

    /// <summary>Everyone who can be picked as a member (including the unknown user, to try the error handling).</summary>
    public static readonly IReadOnlyList<TestUser> All = [.. CanLogIn, UnknownUser];

    public static string NameOf(Guid id) => All.FirstOrDefault(u => u.Id == id)?.Name ?? id.ToString()[..8];

    private static TestUser User(int number, string name) =>
        new(Guid.Parse($"30000000-0000-0000-0000-{number:D12}"), name);
}

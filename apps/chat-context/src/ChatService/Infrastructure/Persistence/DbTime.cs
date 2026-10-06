namespace ChatService.Infrastructure.Persistence;

/// <summary>
/// Npgsql kræver UTC ved skrivning til timestamptz og returnerer DateTime (UTC) ved læsning.
/// </summary>
internal static class DbTime
{
    public static DateTimeOffset ToDb(DateTimeOffset value) => value.ToUniversalTime();

    public static DateTimeOffset? ToDb(DateTimeOffset? value) => value?.ToUniversalTime();

    public static DateTimeOffset FromDb(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTimeOffset? FromDb(DateTime? value) =>
        value is null ? null : FromDb(value.Value);
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ChatService.Tests.Api;

internal sealed record LogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Payload);

/// <summary>
/// Records log lines with the scope Bizcord.Logging puts the payload in. Entries are shared by the tests in a class -
/// find "your" entry by id.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, object?>?> CurrentScope = new();

    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        var previous = CurrentScope.Value;
        CurrentScope.Value = state as IReadOnlyDictionary<string, object?>;
        return new RestoreScope(() => CurrentScope.Value = previous);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), CurrentScope.Value ?? new Dictionary<string, object?>()));

    private sealed class RestoreScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

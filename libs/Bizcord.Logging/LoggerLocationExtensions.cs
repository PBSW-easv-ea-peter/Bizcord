using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Bizcord.Logging;

/// <summary>
/// ILogger doesn't know the call site. The caller attributes are filled in by the compiler at the call and put in a scope,
/// which Serilog turns into properties. Payload goes in the same scope.
/// params object[] can't be combined with caller attributes (params must come last) - hence a fixed signature.
/// </summary>
public static class LoggerLocationExtensions
{
    public static void Information(this ILogger logger, string message, object? payload = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = "") =>
        Write(logger, LogLevel.Information, message, payload, null, filePath, lineNumber, memberName);

    public static void Warning(this ILogger logger, string message, object? payload = null, Exception? exception = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = "") =>
        Write(logger, LogLevel.Warning, message, payload, exception, filePath, lineNumber, memberName);

    public static void Error(this ILogger logger, string message, object? payload = null, Exception? exception = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = "") =>
        Write(logger, LogLevel.Error, message, payload, exception, filePath, lineNumber, memberName);

    private static void Write(ILogger logger, LogLevel level, string message, object? payload, Exception? exception,
        string filePath, int lineNumber, string memberName)
    {
        if (!logger.IsEnabled(level))
            return;

        var state = new Dictionary<string, object?>
        {
            [LogProperties.FilePath] = filePath,
            [LogProperties.LineNumber] = lineNumber,
            [LogProperties.MemberName] = memberName
        };

        if (payload is not null)
        {
            // Anonymous objects are flattened into properties, so they end up as fields in Payload.
            foreach (var property in payload.GetType().GetProperties())
                state[property.Name] = property.GetValue(payload);
        }

        using (logger.BeginScope(state))
        {
            // The message is plain text, not a message template - curly braces are escaped.
            logger.Log(level, exception, message.Replace("{", "{{").Replace("}", "}}"));
        }
    }
}

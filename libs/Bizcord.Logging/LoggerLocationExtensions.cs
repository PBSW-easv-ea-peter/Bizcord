using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Bizcord.Logging;

/// <summary>
/// ILogger kender ikke kaldestedet. Caller-attributterne udfyldes af compileren ved kaldet og lægges i et scope,
/// som Serilog gør til properties. Payload lægges i samme scope.
/// params object[] kan ikke kombineres med caller-attributter (params skal stå sidst) - derfor en fast signatur.
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
            // Anonyme objekter foldes ud til properties, så de ender som felter i Payload.
            foreach (var property in payload.GetType().GetProperties())
                state[property.Name] = property.GetValue(payload);
        }

        using (logger.BeginScope(state))
        {
            // Beskeden er fast tekst, ikke et message template - krøllede parenteser escapes.
            logger.Log(level, exception, message.Replace("{", "{{").Replace("}", "}}"));
        }
    }
}

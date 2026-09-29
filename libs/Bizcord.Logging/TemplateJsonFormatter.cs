using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;

namespace Bizcord.Logging;

/// <summary>
/// Skriver én JSON-linje pr. log-event i formen fra docs/logging-template.json:
/// Timestamp, Level, Location, Tracing, Message, Payload. Alt der ikke har et fast felt, havner i Payload.
/// </summary>
public sealed class TemplateJsonFormatter : ITextFormatter
{
    private static readonly JsonValueFormatter ValueFormatter = new(typeTagName: null);

    public void Format(LogEvent logEvent, TextWriter output)
    {
        output.Write("{\"Timestamp\":\"");
        output.Write(logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        output.Write("\",\"Level\":\"");
        output.Write(logEvent.Level);

        output.Write("\",\"Location\":{");
        WriteField(logEvent, LogProperties.Service, output);
        output.Write(',');
        WriteField(logEvent, LogProperties.FilePath, output);
        output.Write(',');
        WriteField(logEvent, LogProperties.LineNumber, output);
        output.Write(',');
        WriteField(logEvent, LogProperties.MemberName, output);

        output.Write("},\"Tracing\":{\"TraceId\":");
        WriteString(logEvent.TraceId?.ToHexString(), output);
        output.Write(",\"SpanId\":");
        WriteString(logEvent.SpanId?.ToHexString(), output);
        output.Write(',');
        WriteField(logEvent, LogProperties.ParentId, output);

        output.Write("},\"Message\":");
        JsonValueFormatter.WriteQuotedJsonString(logEvent.RenderMessage(), output);

        output.Write(",\"Payload\":{");
        var first = true;
        foreach (var (name, value) in logEvent.Properties)
        {
            if (LogProperties.Reserved.Contains(name))
                continue;

            if (!first)
                output.Write(',');
            first = false;

            JsonValueFormatter.WriteQuotedJsonString(name, output);
            output.Write(':');
            ValueFormatter.Format(value, output);
        }

        if (logEvent.Exception is { } ex)
        {
            if (!first)
                output.Write(',');

            // Skabelonen har intet exception-felt - det lægges i Payload, så top-niveauet forbliver låst.
            output.Write("\"Exception\":{\"Type\":");
            JsonValueFormatter.WriteQuotedJsonString(ex.GetType().FullName ?? ex.GetType().Name, output);
            output.Write(",\"Message\":");
            JsonValueFormatter.WriteQuotedJsonString(ex.Message, output);
            output.Write(",\"StackTrace\":");
            WriteString(ex.StackTrace, output);
            output.Write('}');
        }

        output.Write("}}");
        output.WriteLine();
    }

    private static void WriteField(LogEvent logEvent, string name, TextWriter output)
    {
        JsonValueFormatter.WriteQuotedJsonString(name, output);
        output.Write(':');

        if (logEvent.Properties.TryGetValue(name, out var value))
            ValueFormatter.Format(value, output);
        else
            output.Write("null");
    }

    private static void WriteString(string? value, TextWriter output)
    {
        if (value is null)
            output.Write("null");
        else
            JsonValueFormatter.WriteQuotedJsonString(value, output);
    }
}

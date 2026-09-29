using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Bizcord.Logging.Tests;

public class TemplateJsonFormatterTests
{
    /// <summary>Samme pipeline som AddBizcordLogging, men til en StringWriter i stedet for stdout.</summary>
    private static (Microsoft.Extensions.Logging.ILogger Logger, Func<JsonElement> Output) CreateLogger()
    {
        var sink = new StringWriterSink();
        var serilog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "TestService")
            .Enrich.With<ActivityParentEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var logger = new SerilogLoggerProvider(serilog, dispose: true).CreateLogger("Test");
        return (logger, () => JsonDocument.Parse(sink.Single()).RootElement);
    }

    [Fact]
    public void Output_has_the_template_shape()
    {
        var (logger, output) = CreateLogger();

        logger.Information("Hello");

        var json = output();
        Assert.Equal(["Timestamp", "Level", "Location", "Tracing", "Message", "Payload"], json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["Service", "FilePath", "LineNumber", "MemberName"], json.GetProperty("Location").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["TraceId", "SpanId", "ParentId"], json.GetProperty("Tracing").EnumerateObject().Select(p => p.Name));
        Assert.Equal("Information", json.GetProperty("Level").GetString());
        Assert.Equal("Hello", json.GetProperty("Message").GetString());
        Assert.Equal("TestService", json.GetProperty("Location").GetProperty("Service").GetString());
    }

    [Fact]
    public void Location_is_the_call_site()
    {
        var (logger, output) = CreateLogger();

        logger.Information("Hello"); var expectedLine = new StackFrame(0, true).GetFileLineNumber();

        var location = output().GetProperty("Location");
        Assert.Equal(nameof(Location_is_the_call_site), location.GetProperty("MemberName").GetString());
        Assert.Equal(expectedLine, location.GetProperty("LineNumber").GetInt32());
        Assert.EndsWith("TemplateJsonFormatterTests.cs", location.GetProperty("FilePath").GetString());
    }

    [Fact]
    public void Tracing_comes_from_the_current_activity()
    {
        var (logger, output) = CreateLogger();
        using var listener = ListenToAll();
        using var source = new ActivitySource("Test");

        using var parent = source.StartActivity("parent")!;
        using var child = source.StartActivity("child")!;
        logger.Information("Inside child");

        var tracing = output().GetProperty("Tracing");
        Assert.Equal(child.TraceId.ToHexString(), tracing.GetProperty("TraceId").GetString());
        Assert.Equal(child.SpanId.ToHexString(), tracing.GetProperty("SpanId").GetString());
        Assert.Equal(parent.SpanId.ToHexString(), tracing.GetProperty("ParentId").GetString());
    }

    [Fact]
    public void Tracing_is_null_without_an_activity()
    {
        var (logger, output) = CreateLogger();

        logger.Information("No activity");

        var tracing = output().GetProperty("Tracing");
        Assert.Equal(JsonValueKind.Null, tracing.GetProperty("TraceId").ValueKind);
        Assert.Equal(JsonValueKind.Null, tracing.GetProperty("ParentId").ValueKind);
    }

    [Fact]
    public void Payload_properties_go_under_Payload_and_not_top_level()
    {
        var (logger, output) = CreateLogger();
        var chatId = Guid.NewGuid();

        logger.Information("Message sent.", new { ChatId = chatId, Count = 3 });

        var json = output();
        var payload = json.GetProperty("Payload");
        Assert.Equal(chatId.ToString(), payload.GetProperty("ChatId").GetString());
        Assert.Equal(3, payload.GetProperty("Count").GetInt32());
        Assert.False(json.TryGetProperty("ChatId", out _));
        Assert.False(payload.TryGetProperty("MemberName", out _));
    }

    [Fact]
    public void Exception_goes_under_Payload()
    {
        var (logger, output) = CreateLogger();

        logger.Error("Publish failed.", new { EventType = "MessageSent" }, new InvalidOperationException("Broker down"));

        var json = output();
        Assert.Equal("Error", json.GetProperty("Level").GetString());
        var exception = json.GetProperty("Payload").GetProperty("Exception");
        Assert.Equal("System.InvalidOperationException", exception.GetProperty("Type").GetString());
        Assert.Equal("Broker down", exception.GetProperty("Message").GetString());
    }

    [Fact]
    public void Braces_in_message_are_literal_text()
    {
        var (logger, output) = CreateLogger();

        logger.Warning("Chat '{abc}' was not found.");

        Assert.Equal("Chat '{abc}' was not found.", output().GetProperty("Message").GetString());
    }

    private static ActivityListener ListenToAll()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class StringWriterSink : ILogEventSink
    {
        private readonly TemplateJsonFormatter _formatter = new();
        private readonly List<string> _lines = [];

        public void Emit(LogEvent logEvent)
        {
            var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            _lines.Add(writer.ToString());
        }

        public string Single() => Assert.Single(_lines);
    }
}

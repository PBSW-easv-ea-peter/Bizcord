using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Bizcord.Logging;

/// <summary>
/// Serilog captures TraceId and SpanId from Activity.Current by itself, but not the parent.
/// A root activity has ParentSpanId = 0000000000000000 - it is omitted.
/// </summary>
public sealed class ActivityParentEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var parent = Activity.Current?.ParentSpanId ?? default;
        if (parent != default)
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(LogProperties.ParentId, parent.ToHexString()));
    }
}

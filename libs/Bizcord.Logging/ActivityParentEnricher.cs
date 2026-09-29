using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Bizcord.Logging;

/// <summary>
/// Serilog fanger selv TraceId og SpanId fra Activity.Current, men ikke parent.
/// En rod-activity har ParentSpanId = 0000000000000000 - den udelades.
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

using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Observability;

// Copies the ids OpenTelemetry assigns to the current request onto every
// log entry, so a log entry in Seq can be matched to a trace in Zipkin.
internal class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        logEvent.AddPropertyIfAbsent(
            factory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(
            factory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
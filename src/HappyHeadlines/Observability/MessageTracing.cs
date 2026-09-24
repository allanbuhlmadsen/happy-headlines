using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Observability;

// Carries the trace across a queue: the sender writes the trace id into the
// message headers, and the receiver reads it back out and continues the same
// trace instead of starting a new one.
public static class MessageTracing
{
    // Every service that sends or receives messages uses this same name,
    // so the spans show up under one activity source.
    public const string ActivitySourceName = "HappyHeadlines.Messaging";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly TextMapPropagator Propagator =
        Propagators.DefaultTextMapPropagator;

    // Called by the sender, with the headers of the message about to be sent.
    public static void Inject(Activity? activity, IDictionary<string, object?> headers)
    {
        if (activity is null) return;

        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            headers,
            static (carrier, key, value) => carrier[key] = Encoding.UTF8.GetBytes(value));
    }

    // Called by the receiver, with the headers of the message just received.
    public static PropagationContext Extract(IDictionary<string, object?>? headers)
    {
        if (headers is null) return default;

        return Propagator.Extract(
            default,
            headers,
            static (carrier, key) =>
            {
                if (!carrier.TryGetValue(key, out var value)) return [];
                if (value is byte[] bytes) return [Encoding.UTF8.GetString(bytes)];
                return [value?.ToString() ?? string.Empty];
            });
    }
}
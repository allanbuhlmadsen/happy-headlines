using Prometheus;

namespace Observability;

// Counts what each cache does, so Prometheus can read the numbers and
// Grafana can show the hit ratio. Every cache creates one of these with
// its own name.
public class CacheMetrics
{
    private static readonly Counter Lookups = Metrics.CreateCounter(
        "happyheadlines_cache_lookups_total",
        "Cache lookups, labelled by cache and by whether they were a hit.",
        new CounterConfiguration
        {
            LabelNames = ["cache", "result"]
        });

    private static readonly Gauge Entries = Metrics.CreateGauge(
        "happyheadlines_cache_entries",
        "How many entries the cache holds right now.",
        new GaugeConfiguration
        {
            LabelNames = ["cache"]
        });

    private readonly string _cacheName;

    public CacheMetrics(string cacheName)
    {
        _cacheName = cacheName;
    }

    // The lookup was answered from the cache.
    public void RecordHit() => Lookups.WithLabels(_cacheName, "hit").Inc();

    // The cache did not have it, so the database had to be asked.
    public void RecordMiss() => Lookups.WithLabels(_cacheName, "miss").Inc();

    public void SetEntries(int count) => Entries.WithLabels(_cacheName).Set(count);
}
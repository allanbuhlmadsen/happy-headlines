using ArticleService.Models;
using Observability;

namespace ArticleService.Caching;

// Holds the articles the offline process has fetched from the global
// database. Nothing is evicted one at a time: the whole contents are
// replaced every time the offline process runs.
public class ArticleCache
{
    public const string CacheName = "article";

    private readonly CacheMetrics _metrics = new(CacheName);
    private readonly ILogger<ArticleCache> _log;

    // Readers and the offline process touch the cache at the same time,
    // so every access is guarded.
    private readonly object _gate = new();

    private Dictionary<int, Article> _articles = new();

    public ArticleCache(ILogger<ArticleCache> log)
    {
        _log = log;
    }

    public DateTime? FilledAt { get; private set; }

    public int Count
    {
        get { lock (_gate) { return _articles.Count; } }
    }

    // Asked by the controller before it goes to the database.
    public bool TryGet(int id, out Article? article)
    {
        lock (_gate)
        {
            if (_articles.TryGetValue(id, out var found))
            {
                _metrics.RecordHit();
                article = found;
                return true;
            }
        }

        _metrics.RecordMiss();
        article = null;
        return false;
    }

    // Called by the offline process with everything it fetched.
    public void Replace(IEnumerable<Article> articles)
    {
        var fresh = articles.ToDictionary(a => a.Id);

        lock (_gate)
        {
            _articles = fresh;
            FilledAt = DateTime.UtcNow;
        }

        _metrics.SetEntries(fresh.Count);

        _log.LogInformation("Filled the article cache with {ArticleCount} articles",
            fresh.Count);
    }
}
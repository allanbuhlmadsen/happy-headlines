using CommentService.Models;
using Observability;

namespace CommentService.Caching;

// Holds all comments for the most recently accessed articles. It is filled
// on a cache miss, and when it is full the least recently used article is
// thrown out — the LRU algorithm.
public class CommentCache
{
    public const string CacheName = "comment";

    // One article's comments, and the key they are stored under.
    private sealed class Entry
    {
        public required string Key { get; init; }
        public required List<Comment> Comments { get; set; }
    }

    private readonly CacheMetrics _metrics = new(CacheName);
    private readonly ILogger<CommentCache> _log;
    private readonly int _capacity;

    private readonly object _gate = new();

    // The order of use. The first node is the most recently used article,
    // the last node is the one thrown out when the cache is full.
    private readonly LinkedList<Entry> _order = new();

    // Finds an article's place in the order without traversing the whole list.
    private readonly Dictionary<string, LinkedListNode<Entry>> _byKey = new();

    public CommentCache(IConfiguration config, ILogger<CommentCache> log)
    {
        _log = log;
        _capacity = config.GetValue<int?>("CommentCache:Articles") ?? 30;

        _log.LogInformation("The comment cache holds the comments of {Capacity} articles",
            _capacity);
    }

    // Article ids are only unique within one continent, so the key needs both.
    public static string KeyFor(string continent, int articleId) =>
        $"{continent.ToLowerInvariant()}/{articleId}";

    public int Count
    {
        get { lock (_gate) { return _byKey.Count; } }
    }

    // Asked before the database. A hit also counts as "used just now".
    public bool TryGet(string key, out List<Comment>? comments)
    {
        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var node))
            {
                // Used now, so it moves to the front and becomes the last
                // one to be thrown out.
                _order.Remove(node);
                _order.AddFirst(node);

                _metrics.RecordHit();
                comments = node.Value.Comments;
                return true;
            }
        }

        _metrics.RecordMiss();
        comments = null;
        return false;
    }

    // Called after a miss, with what the database answered.
    public void Set(string key, List<Comment> comments)
    {
        string? evicted = null;

        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var existing))
            {
                existing.Value.Comments = comments;
                _order.Remove(existing);
                _order.AddFirst(existing);
            }
            else
            {
                var node = _order.AddFirst(new Entry { Key = key, Comments = comments });
                _byKey[key] = node;

                if (_byKey.Count > _capacity)
                {
                    var last = _order.Last!;
                    _order.RemoveLast();
                    _byKey.Remove(last.Value.Key);
                    evicted = last.Value.Key;
                }
            }

            _metrics.SetEntries(_byKey.Count);
        }

        if (evicted is not null)
        {
            _log.LogInformation(
                "The comment cache was full, so {EvictedKey} was removed as the least recently used",
                evicted);
        }
    }

    // Called when a comment is posted or deleted, so the next reader does
    // not get the old list back.
    public void Remove(string key)
    {
        lock (_gate)
        {
            if (_byKey.Remove(key, out var node))
            {
                _order.Remove(node);
                _metrics.SetEntries(_byKey.Count);
            }
        }
    }
}
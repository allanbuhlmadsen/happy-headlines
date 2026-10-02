using ArticleService.Data;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Caching;

// The offline process. It fills the cache with the articles the global
// database published in the last 14 days, over and over, whether or not
// anyone has asked for them.
public class ArticleCacheRefresher : BackgroundService
{
    private const string Continent = "global";

    private readonly ArticleCache _cache;
    private readonly ContinentDbContextFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<ArticleCacheRefresher> _log;

    public ArticleCacheRefresher(
        ArticleCache cache,
        ContinentDbContextFactory factory,
        IConfiguration config,
        ILogger<ArticleCacheRefresher> log)
    {
        _cache = cache;
        _factory = factory;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var days = _config.GetValue<int?>("ArticleCache:Days") ?? 14;
        var seconds = _config.GetValue<int?>("ArticleCache:RefreshSeconds") ?? 60;

        _log.LogInformation(
            "Refreshing the article cache every {RefreshSeconds} seconds with the last {Days} days",
            seconds, days);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var since = DateTime.UtcNow.AddDays(-days);

                await using var db = _factory.Create(Continent);

                var articles = await db.Articles
                    .Where(a => a.PublishedAt >= since)
                    .AsNoTracking()
                    .ToListAsync(stoppingToken);

                _cache.Replace(articles);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Could not refresh the article cache");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
using FeatureHubSDK;
using Microsoft.AspNetCore.Mvc;
using NewsletterService.Models;
using NewsletterService.Services;
using Polly.CircuitBreaker;

namespace NewsletterService.Controllers;

public record NewsletterSummary(string Continent, int Articles, int Recipients);

[ApiController]
[Route("newsletter")]
public class NewsletterController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly SubscriberClient _subscribers;
    private readonly IClientContext _features;
    private readonly ILogger<NewsletterController> _log;

    public NewsletterController(
        IHttpClientFactory factory,
        SubscriberClient subscribers,
        IClientContext features,
        ILogger<NewsletterController> log)
    {
        _http = factory.CreateClient("articles");
        _subscribers = subscribers;
        _features = features;
        _log = log;
    }

    // Assembles the daily newsletter for one continent by asking
    // ArticleService for its articles.
    [HttpGet("{continent}")]
    public async Task<ActionResult<IEnumerable<Article>>> Get(string continent)
    {
        var response = await _http.GetAsync($"/continents/{continent}/articles");

        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning(
                "ArticleService answered {StatusCode} for the {Continent} newsletter",
                (int)response.StatusCode, continent);
            return StatusCode(503, "Articles could not be fetched right now.");
        }

        var articles = await response.Content.ReadFromJsonAsync<List<Article>>()
                       ?? new List<Article>();

        _log.LogInformation(
            "Assembled the {Continent} newsletter from {ArticleCount} articles",
            continent, articles.Count);

        return articles;
    }

    // Sends the newsletter: the articles of the continent to every subscriber.
    [HttpPost("{continent}/send")]
    public async Task<ActionResult<NewsletterSummary>> Send(string continent)
    {
        var response = await _http.GetAsync($"/continents/{continent}/articles");

        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning(
                "ArticleService answered {StatusCode} for the {Continent} newsletter",
                (int)response.StatusCode, continent);
            return StatusCode(503, "Articles could not be fetched right now.");
        }

        var articles = await response.Content.ReadFromJsonAsync<List<Article>>()
                       ?? new List<Article>();

        if (!_features["SUBSCRIBERS_ENABLED"].IsEnabled)
        {
            _log.LogInformation(
                "The subscriber feature is turned off, so the {Continent} newsletter was not sent",
                continent);
            return StatusCode(503, "The subscriber feature is turned off.");
        }

        List<Subscriber> subscribers;

        try
        {
            subscribers = await _subscribers.GetAllAsync();
        }
        catch (BrokenCircuitException)
        {
            _log.LogWarning("The circuit to SubscriberService is open");
            return StatusCode(503,
                "The newsletter cannot be sent because the subscriber list " +
                "cannot be reached. Please try again shortly.");
        }
        catch (HttpRequestException)
        {
            _log.LogWarning("SubscriberService could not be reached");
            return StatusCode(503,
                "The newsletter cannot be sent because the subscriber list " +
                "cannot be reached. Please try again shortly.");
        }

        _log.LogInformation(
            "Sent the {Continent} newsletter with {ArticleCount} articles to {RecipientCount} subscribers",
            continent, articles.Count, subscribers.Count);

        return new NewsletterSummary(continent, articles.Count, subscribers.Count);
    }
}
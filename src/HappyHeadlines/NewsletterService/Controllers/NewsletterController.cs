using Microsoft.AspNetCore.Mvc;
using NewsletterService.Models;

namespace NewsletterService.Controllers;

[ApiController]
[Route("newsletter")]
public class NewsletterController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly ILogger<NewsletterController> _log;

    public NewsletterController(IHttpClientFactory factory, ILogger<NewsletterController> log)
    {
        _http = factory.CreateClient("articles");
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
}
using Microsoft.AspNetCore.Mvc;
using PublisherService.Messaging;
using PublisherService.Models;

namespace PublisherService.Controllers;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    private readonly ArticlePublisher _publisher;
    private readonly ILogger<ArticlesController> _log;

    public ArticlesController(ArticlePublisher publisher, ILogger<ArticlesController> log)
    {
        _publisher = publisher;
        _log = log;
    }

    [HttpPost]
    public async Task<IActionResult> Publish(Article article)
    {
        if (string.IsNullOrWhiteSpace(article.Title))
        {
            _log.LogWarning("Rejected an article from {Author}: the title was empty",
                article.Author);
            return BadRequest("Article title cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(article.Continent))
        {
            _log.LogWarning("Rejected the article {Title}: no continent was given",
                article.Title);
            return BadRequest("Article continent cannot be empty.");
        }

        await _publisher.PublishAsync(article);

        _log.LogInformation("Placed the article {Title} for {Continent} on the queue",
            article.Title, article.Continent);

        return Accepted();
    }
}
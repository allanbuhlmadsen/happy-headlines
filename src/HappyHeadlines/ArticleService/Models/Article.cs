namespace ArticleService.Models;

public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }

    // Set by the service from the route — never accepted from the caller.
    public string Continent { get; set; } = string.Empty;
}
namespace CommentService.Models;

public class Comment
{
    public int Id { get; set; }

    // Together these identify an article: article ids are only unique
    // within one continent's database.
    public string ArticleContinent { get; set; } = string.Empty;
    public int ArticleId { get; set; }

    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
}
namespace DraftService.Models;

public class Draft
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;

    // Set by the service, never by the caller.
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
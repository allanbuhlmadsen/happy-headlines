using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Webapp.Pages;

public class IndexModel : PageModel
{
    private readonly HttpClient _http;
    private readonly ILogger<IndexModel> _log;

    public IndexModel(IHttpClientFactory factory, ILogger<IndexModel> log)
    {
        _http = factory.CreateClient("publisher");
        _log = log;
    }

    // The values typed into the form come back in these properties.
    [BindProperty]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    public string Body { get; set; } = string.Empty;

    [BindProperty]
    public string Author { get; set; } = string.Empty;

    [BindProperty]
    public string Continent { get; set; } = "europe";

    // Shown above the form after the publisher presses the button.
    public string? Message { get; set; }
    public bool Failed { get; set; }

    public void OnGet()
    {
    }

    public async Task OnPostAsync()
    {
        var article = new
        {
            title = Title,
            body = Body,
            author = Author,
            continent = Continent
        };

        var response = await _http.PostAsJsonAsync("/articles", article);

        if (response.IsSuccessStatusCode)
        {
            _log.LogInformation("Sent the article {Title} for {Continent} to PublisherService",
                Title, Continent);

            Message = $"The article \"{Title}\" was sent for publication.";
            Title = string.Empty;
            Body = string.Empty;
        }
        else
        {
            _log.LogWarning("PublisherService answered {StatusCode} for the article {Title}",
                (int)response.StatusCode, Title);

            Message = "The article could not be sent. Try again.";
            Failed = true;
        }
    }
}
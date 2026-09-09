namespace CommentService.Services;

public record FilterRequest(string Text);
public record FilterResponse(string Text, int ReplacedCount);

public class ProfanityClient
{
    private readonly HttpClient _http;

    public ProfanityClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> FilterAsync(string text, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("profanity/filter",
            new FilterRequest(text), ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FilterResponse>(ct);

        if (result is null)
            throw new InvalidOperationException("ProfanityService returned an empty response.");

        return result.Text;
    }
}
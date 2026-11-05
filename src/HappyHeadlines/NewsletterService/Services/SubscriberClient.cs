using NewsletterService.Models;

namespace NewsletterService.Services;

public class SubscriberClient
{
    private readonly HttpClient _http;

    public SubscriberClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Subscriber>> GetAllAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("subscribers", ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<Subscriber>>(ct)
               ?? new List<Subscriber>();
    }
}
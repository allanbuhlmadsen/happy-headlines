using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data;

public class ContinentDbContextFactory
{
    private readonly string _template;
    private readonly HashSet<string> _continents;

    public ContinentDbContextFactory(IConfiguration configuration)
    {
        _template = configuration["Databases:Template"]
            ?? throw new InvalidOperationException("Databases:Template is not configured.");

        var list = configuration["Databases:Continents"]
            ?? throw new InvalidOperationException("Databases:Continents is not configured.");

        _continents = list
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToLowerInvariant())
            .ToHashSet();
    }

    public IReadOnlyCollection<string> Continents => _continents;

    public bool IsValid(string continent) =>
        _continents.Contains(continent.ToLowerInvariant());

    public ArticleDbContext Create(string continent)
    {
        var key = continent.ToLowerInvariant();

        if (!_continents.Contains(key))
            throw new ArgumentException($"Unknown continent: {continent}", nameof(continent));

        var connectionString = _template.Replace("{host}", $"db-{key}");

        var options = new DbContextOptionsBuilder<ArticleDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ArticleDbContext(options);
    }
}
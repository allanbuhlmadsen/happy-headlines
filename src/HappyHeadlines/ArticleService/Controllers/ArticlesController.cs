using ArticleService.Data;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Controllers;

[ApiController]
[Route("continents/{continent}/articles")]
public class ArticlesController : ControllerBase
{
    private readonly ContinentDbContextFactory _factory;

    public ArticlesController(ContinentDbContextFactory factory)
    {
        _factory = factory;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Article>>> GetAll(string continent)
    {
        if (!_factory.IsValid(continent)) return NotFound();

        using var db = _factory.Create(continent);
        return await db.Articles.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Article>> GetById(string continent, int id)
    {
        if (!_factory.IsValid(continent)) return NotFound();

        using var db = _factory.Create(continent);
        var article = await db.Articles.FindAsync(id);
        if (article is null) return NotFound();
        return article;
    }

    [HttpPost]
    public async Task<ActionResult<Article>> Create(string continent, Article article)
    {
        if (!_factory.IsValid(continent)) return NotFound();

        using var db = _factory.Create(continent);

        // Id and Continent are derived, never taken from the caller.
        article.Id = 0;
        article.Continent = continent.ToLowerInvariant();

        db.Articles.Add(article);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { continent, id = article.Id }, article);
    }

    // Copies only the caller-editable fields. Id and Continent are deliberately
    // left out: Id identifies the row, and Continent is derived from the route.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string continent, int id, Article article)
    {
        if (!_factory.IsValid(continent)) return NotFound();

        using var db = _factory.Create(continent);
        var existing = await db.Articles.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Title = article.Title;
        existing.Body = article.Body;
        existing.Author = article.Author;
        existing.PublishedAt = article.PublishedAt;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string continent, int id)
    {
        if (!_factory.IsValid(continent)) return NotFound();

        using var db = _factory.Create(continent);
        var article = await db.Articles.FindAsync(id);
        if (article is null) return NotFound();

        db.Articles.Remove(article);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
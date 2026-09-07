using ArticleService.Data;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Controllers;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    private readonly ArticleDbContext _db;

    public ArticlesController(ArticleDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Article>>> GetAll()
    {
        return await _db.Articles.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Article>> GetById(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article is null) return NotFound();
        return article;
    }

    // TODO: Id and Continent are currently accepted from the caller. Id is
    // overwritten by the database; Continent will be set from the route once
    // the continent-aware endpoints are in place.
    [HttpPost]
    public async Task<ActionResult<Article>> Create(Article article)
    {
        _db.Articles.Add(article);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = article.Id }, article);
    }

    // Copies only the caller-editable fields. Id and Continent are deliberately
    // left out: Id identifies the row, and Continent is derived from the route.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Article article)
    {
        var existing = await _db.Articles.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Title = article.Title;
        existing.Body = article.Body;
        existing.Author = article.Author;
        existing.PublishedAt = article.PublishedAt;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article is null) return NotFound();

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
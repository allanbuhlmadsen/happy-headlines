using CommentService.Caching;
using CommentService.Data;
using CommentService.Models;
using CommentService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;

namespace CommentService.Controllers;

public record CreateCommentRequest(string Author, string Body);

[ApiController]
[Route("articles/{continent}/{articleId}/comments")]
public class CommentsController : ControllerBase
{
    private readonly CommentDbContext _db;
    private readonly ProfanityClient _profanity;
    private readonly CommentCache _cache;

    public CommentsController(
        CommentDbContext db, ProfanityClient profanity, CommentCache cache)
    {
        _db = db;
        _profanity = profanity;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Comment>>> GetForArticle(
    string continent, int articleId)
    {
        var key = CommentCache.KeyFor(continent, articleId);

        if (_cache.TryGet(key, out var cached))
        {
            return cached!;
        }

        // A miss, so the database is asked and the answer is kept.
        var comments = await _db.Comments
            .Where(c => c.ArticleContinent == continent.ToLowerInvariant()
                     && c.ArticleId == articleId)
            .OrderBy(c => c.PostedAt)
            .ToListAsync();

        _cache.Set(key, comments);

        return comments;
    }

    [HttpPost]
    public async Task<ActionResult<Comment>> Post(
        string continent, int articleId, CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("Comment body cannot be empty.");

        string filteredBody;
        try
        {
            filteredBody = await _profanity.FilterAsync(request.Body);
        }
        catch (BrokenCircuitException)
        {
            return StatusCode(503,
                "Comments are temporarily unavailable because the profanity filter " +
                "cannot be reached. Please try again shortly.");
        }
        catch (HttpRequestException)
        {
            return StatusCode(503,
                "Comments are temporarily unavailable because the profanity filter " +
                "cannot be reached. Please try again shortly.");
        }

        var comment = new Comment
        {
            ArticleContinent = continent.ToLowerInvariant(),
            ArticleId = articleId,
            Author = request.Author,
            Body = filteredBody,
            PostedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        _cache.Remove(CommentCache.KeyFor(continent, articleId));

        return CreatedAtAction(nameof(GetForArticle),
            new { continent, articleId }, comment);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string continent, int articleId, int id)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment is null) return NotFound();

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
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

    public CommentsController(CommentDbContext db, ProfanityClient profanity)
    {
        _db = db;
        _profanity = profanity;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Comment>>> GetForArticle(
        string continent, int articleId)
    {
        return await _db.Comments
            .Where(c => c.ArticleContinent == continent.ToLowerInvariant()
                     && c.ArticleId == articleId)
            .OrderBy(c => c.PostedAt)
            .ToListAsync();
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
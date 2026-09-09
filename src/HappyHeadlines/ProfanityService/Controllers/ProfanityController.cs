using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using ProfanityService.Models;
using System.Text.RegularExpressions;

namespace ProfanityService.Controllers;

public record FilterRequest(string Text);
public record FilterResponse(string Text, int ReplacedCount);

[ApiController]
[Route("profanity")]
public class ProfanityController : ControllerBase
{
    private readonly ProfanityDbContext _db;

    public ProfanityController(ProfanityDbContext db)
    {
        _db = db;
    }

    // Replaces each prohibited word with asterisks, one per letter.
    // Whole words only: "damn" is caught, "damned" is not.
    [HttpPost("filter")]
    public async Task<ActionResult<FilterResponse>> Filter(FilterRequest request)
    {
        var words = await _db.Words.Select(w => w.Word).ToListAsync();

        var text = request.Text;
        var replaced = 0;

        foreach (var word in words)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            text = Regex.Replace(text, pattern, match =>
            {
                replaced++;
                return new string('*', match.Length);
            }, RegexOptions.IgnoreCase);
        }

        return new FilterResponse(text, replaced);
    }

    [HttpGet("words")]
    public async Task<ActionResult<IEnumerable<ProfanityWord>>> GetWords()
    {
        return await _db.Words.OrderBy(w => w.Word).ToListAsync();
    }

    [HttpPost("words")]
    public async Task<ActionResult<ProfanityWord>> AddWord(ProfanityWord word)
    {
        word.Id = 0;
        word.Word = word.Word.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(word.Word))
            return BadRequest("Word cannot be empty.");

        _db.Words.Add(word);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWords), new { id = word.Id }, word);
    }

    [HttpDelete("words/{id}")]
    public async Task<IActionResult> DeleteWord(int id)
    {
        var word = await _db.Words.FindAsync(id);
        if (word is null) return NotFound();

        _db.Words.Remove(word);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
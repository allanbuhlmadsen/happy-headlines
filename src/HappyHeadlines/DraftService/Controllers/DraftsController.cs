using DraftService.Data;
using DraftService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DraftService.Controllers;

public record DraftRequest(string Title, string Body, string Author);

[ApiController]
[Route("drafts")]
public class DraftsController : ControllerBase
{
    private readonly DraftDbContext _db;
    private readonly ILogger<DraftsController> _log;

    public DraftsController(DraftDbContext db, ILogger<DraftsController> log)
    {
        _db = db;
        _log = log;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Draft>>> GetAll()
    {
        var drafts = await _db.Drafts
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();

        _log.LogInformation("Returned {DraftCount} drafts", drafts.Count);

        return drafts;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Draft>> GetById(int id)
    {
        var draft = await _db.Drafts.FindAsync(id);
        if (draft is null)
        {
            _log.LogWarning("Draft {DraftId} was requested but does not exist", id);
            return NotFound();
        }

        _log.LogInformation("Returned draft {DraftId}", id);

        return draft;
    }

    [HttpPost]
    public async Task<ActionResult<Draft>> Post(DraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            _log.LogWarning("Rejected a new draft from {Author}: the title was empty",
                request.Author);
            return BadRequest("Draft title cannot be empty.");
        }

        var now = DateTime.UtcNow;

        var draft = new Draft
        {
            Title = request.Title,
            Body = request.Body,
            Author = request.Author,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync();

        _log.LogInformation("Created draft {DraftId} by {Author}",
            draft.Id, draft.Author);

        return CreatedAtAction(nameof(GetById), new { id = draft.Id }, draft);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Draft>> Put(int id, DraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            _log.LogWarning("Rejected an update of draft {DraftId}: the title was empty",
                id);
            return BadRequest("Draft title cannot be empty.");
        }

        var draft = await _db.Drafts.FindAsync(id);
        if (draft is null)
        {
            _log.LogWarning("Draft {DraftId} was updated but does not exist", id);
            return NotFound();
        }

        draft.Title = request.Title;
        draft.Body = request.Body;
        draft.Author = request.Author;
        draft.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _log.LogInformation("Updated draft {DraftId}", id);

        return draft;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var draft = await _db.Drafts.FindAsync(id);
        if (draft is null)
        {
            _log.LogWarning("Draft {DraftId} was deleted but does not exist", id);
            return NotFound();
        }

        _db.Drafts.Remove(draft);
        await _db.SaveChangesAsync();

        _log.LogInformation("Deleted draft {DraftId}", id);

        return NoContent();
    }
}
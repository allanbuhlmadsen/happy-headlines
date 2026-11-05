using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubscriberService.Data;
using SubscriberService.Models;
using SubscriberService.Messaging;

namespace SubscriberService.Controllers;

// Only the two fields the caller may set. Id and SubscribedAt are ours.
public record CreateSubscriberRequest(string Email, string Name);

[ApiController]
[Route("subscribers")]
public class SubscribersController : ControllerBase
{
    private readonly SubscriberDbContext _db;
    private readonly SubscriberPublisher _publisher;
    private readonly ILogger<SubscribersController> _log;

    public SubscribersController(
        SubscriberDbContext db,
        SubscriberPublisher publisher,
        ILogger<SubscribersController> log)
    {
        _db = db;
        _publisher = publisher;
        _log = log;
    }

    // Asked for by NewsletterService when a newsletter is sent.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subscriber>>> GetAll()
    {
        return await _db.Subscribers
            .OrderBy(s => s.SubscribedAt)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Subscriber>> GetById(int id)
    {
        var subscriber = await _db.Subscribers.FindAsync(id);
        if (subscriber is null) return NotFound();
        return subscriber;
    }

    [HttpPost]
    public async Task<ActionResult<Subscriber>> Post(CreateSubscriberRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("An email address is required.");

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Subscribers.AnyAsync(s => s.Email == email))
        {
            _log.LogWarning("{Email} is already subscribed", email);
            return Conflict("That email address is already subscribed.");
        }

        var subscriber = new Subscriber
        {
            Email = email,
            Name = request.Name,
            SubscribedAt = DateTime.UtcNow
        };

        _db.Subscribers.Add(subscriber);
        await _db.SaveChangesAsync();
        await _publisher.PublishAsync(subscriber);

        _log.LogInformation("Subscriber {SubscriberId} signed up", subscriber.Id);

        return CreatedAtAction(nameof(GetById), new { id = subscriber.Id }, subscriber);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var subscriber = await _db.Subscribers.FindAsync(id);
        if (subscriber is null) return NotFound();

        _db.Subscribers.Remove(subscriber);
        await _db.SaveChangesAsync();

        _log.LogInformation("Subscriber {SubscriberId} unsubscribed", id);

        return NoContent();
    }
}
using System.ComponentModel.DataAnnotations;
using FHGM.Backend.Authentication;
using FHGM.Backend.Data;
using FHGM.Backend.DTOs;
using FHGM.Backend.Entities;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/events"), Authorize(Policy = Permissions.ManageEvents)]
public sealed class EventsController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        return Ok(new { total = await db.Events.CountAsync(), page, items = await db.Events.AsNoTracking()
            .OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync() });
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return item is null ? NotFound() : Ok(item);
    }
    [HttpPost]
    public async Task<IActionResult> Create(EventRequest request)
    {
        var item = new MinistryEvent(); Apply(item, request); db.Events.Add(item);
        audit.Record("EventCreated", "MinistryEvent", item.Id.ToString());
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, EventRequest request)
    {
        var item = await db.Events.FindAsync(id);
        if (item is null) return NotFound();
        if (item.Version != request.Version) return Conflict(new { message = "Reload the event; another person changed it." });
        Apply(item, request); item.Version++;
        audit.Record(item.IsPublished ? "EventPublishedOrEdited" : "EventUnpublishedOrEdited", "MinistryEvent", id.ToString());
        await db.SaveChangesAsync(); return Ok(item);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery, Required, Range(1, int.MaxValue)] int? version)
    {
        var item = await db.Events.FindAsync(id);
        if (item is null) return NotFound();
        if (item.Version != version) return Conflict(new { message = "Reload the event before deleting it." });
        db.Events.Remove(item); audit.Record("EventDeleted", "MinistryEvent", id.ToString());
        await db.SaveChangesAsync(); return NoContent();
    }
    private static void Apply(MinistryEvent item, EventRequest request)
    {
        item.Title = request.Title.Trim(); item.Slug = request.Slug; item.Description = request.Description.Trim();
        item.Location = request.Location.Trim(); item.StartsAt = request.StartsAt.ToUniversalTime(); item.EndsAt = request.EndsAt.ToUniversalTime();
        item.IsPublished = request.IsPublished; item.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

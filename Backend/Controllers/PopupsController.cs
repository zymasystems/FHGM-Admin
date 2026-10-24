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

[ApiController, Route("api/popups"), Authorize(Policy = Permissions.ManagePopups)]
public sealed class PopupsController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        return Ok(new { total = await db.Popups.CountAsync(), page, items = await db.Popups.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync() });
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await db.Popups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return item is null ? NotFound() : Ok(item);
    }
    [HttpPost]
    public async Task<IActionResult> Create(PopupRequest request)
    {
        var item = new PopupAnnouncement(); Apply(item, request); db.Popups.Add(item);
        audit.Record("PopupCreated", "PopupAnnouncement", item.Id.ToString());
        await db.SaveChangesAsync(); return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PopupRequest request)
    {
        var item = await db.Popups.FindAsync(id);
        if (item is null) return NotFound();
        if (item.Version != request.Version) return Conflict(new { message = "Reload the announcement; another person changed it." });
        Apply(item, request); item.Version++;
        audit.Record(item.IsPublished ? "PopupPublishedOrEdited" : "PopupUnpublishedOrEdited", "PopupAnnouncement", id.ToString());
        await db.SaveChangesAsync(); return Ok(item);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery, Required, Range(1, int.MaxValue)] int? version)
    {
        var item = await db.Popups.FindAsync(id);
        if (item is null) return NotFound();
        if (item.Version != version) return Conflict(new { message = "Reload the announcement before deleting it." });
        db.Popups.Remove(item); audit.Record("PopupDeleted", "PopupAnnouncement", id.ToString());
        await db.SaveChangesAsync(); return NoContent();
    }
    private static void Apply(PopupAnnouncement item, PopupRequest request)
    {
        item.Title = request.Title.Trim(); item.Message = request.Message.Trim(); item.ButtonText = request.ButtonText.Trim(); item.ButtonUrl = request.ButtonUrl.Trim();
        item.StartsAt = request.StartsAt.ToUniversalTime(); item.EndsAt = request.EndsAt.ToUniversalTime();
        item.IsPublished = request.IsPublished; item.Priority = request.Priority; item.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

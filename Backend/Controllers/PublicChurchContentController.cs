using FHGM.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/public"), AllowAnonymous]
public sealed class PublicChurchContentController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet("events")]
    public async Task<IActionResult> Events(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var now = clock.GetUtcNow();
        var query = db.Events.AsNoTracking().Where(x => x.IsPublished && x.EndsAt > now);
        return Ok(new { total = await query.CountAsync(), page, serverTime = now, items = await query.OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .Skip((page - 1) * 12).Take(12).Select(x => new { x.Title, x.Slug, x.Description, x.Location, x.StartsAt, x.EndsAt }).ToListAsync() });
    }
    [HttpGet("events/{slug}")]
    public async Task<IActionResult> Event(string slug)
    {
        var now = clock.GetUtcNow();
        var item = await db.Events.AsNoTracking().Where(x => x.Slug == slug && x.IsPublished && x.EndsAt > now)
            .Select(x => new { x.Title, x.Slug, x.Description, x.Location, x.StartsAt, x.EndsAt }).FirstOrDefaultAsync();
        return item is null ? NotFound() : Ok(item);
    }
    [HttpGet("popups/active")]
    public async Task<IActionResult> Popups()
    {
        var now = clock.GetUtcNow();
        var items = await db.Popups.AsNoTracking().Where(x => x.IsPublished && x.StartsAt <= now && x.EndsAt > now)
            .OrderByDescending(x => x.Priority).ThenByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Take(10)
            .Select(x => new { x.Id, x.Title, x.Message, x.ButtonText, x.ButtonUrl, x.StartsAt, x.EndsAt, x.Version }).ToListAsync();
        return Ok(new { serverTime = now, items });
    }
}

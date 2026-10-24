using FHGM.Backend.Authentication;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/audit"), Authorize(Policy = Permissions.ViewAudit)]
public sealed class AuditController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var records = await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id)
            .Skip((page - 1) * 50).Take(50).ToListAsync();
        var identifiers = records.Select(x => x.EntityId).Concat(records.Select(x => x.UserId))
            .Where(x => x is not null).Distinct().ToArray();
        var references = await db.AuditReferences.AsNoTracking()
            .Where(x => identifiers.Contains(x.Identifier))
            .ToDictionaryAsync(x => new { x.Entity, x.Identifier }, x => x.Id);

        string? Reference(string entity, string? identifier) => identifier is null ? null
            : FormatReference(entity, references[new { Entity = entity, Identifier = identifier }]);
        return Ok(new { total = await db.AuditLogs.CountAsync(), page,
            items = records.Select(x => new
            {
                x.Id, x.Action, x.Entity,
                EntityId = Reference(x.Entity, x.EntityId),
                UserId = Reference("User", x.UserId),
                x.CreatedAt
            }) });
    }

    private static string FormatReference(string entity, long number)
    {
        var prefix = entity switch
        {
            "User" => "user", "BlogPost" => "blog", "GalleryAlbum" => "album",
            "GalleryImage" => "image", "MinistryEvent" => "event", "PopupAnnouncement" => "popup",
            _ => entity.ToLowerInvariant()
        };
        return prefix + "-" + number.ToString("D2", CultureInfo.InvariantCulture);
    }
}

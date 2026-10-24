using FHGM.Backend.Authentication;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

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
        return Ok(new { total = await db.AuditLogs.CountAsync(), page,
            items = records.Select(x => new
            {
                x.Id, x.Action, x.Entity,
                EntityId = HashIdentifier(x.Entity, x.EntityId),
                UserId = HashIdentifier("User", x.UserId),
                x.CreatedAt
            }) });
    }

    // Hash at the response boundary so historical entries are protected too.
    // Keep full hashes to avoid shortened-reference collisions. These are
    // stable pseudonyms, not anonymization or authorization credentials.
    private static string? HashIdentifier(string entity, string? id) => id is null ? null
        : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"FHGM.Audit.v1:{entity}:{id}")));
}

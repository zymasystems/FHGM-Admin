using FHGM.Backend.Authentication;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/audit"), Authorize(Policy = Permissions.ViewAudit)]
public sealed class AuditController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        return Ok(new { total = await db.AuditLogs.CountAsync(), page,
            items = await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id).Skip((page - 1) * 50).Take(50).ToListAsync() });
    }
}

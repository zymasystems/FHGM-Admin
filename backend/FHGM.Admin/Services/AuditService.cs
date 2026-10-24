using FHGM.Admin.Data;
using FHGM.Admin.Models;
using System.Security.Claims;

namespace FHGM.Admin.Services;

public sealed class AuditService(AppDbContext db, IHttpContextAccessor accessor)
{
    // Queued on the same DbContext so a content change and its audit entry commit atomically.
    public void Record(string action, string entity, string entityId, string? userId = null)
    {
        var context = accessor.HttpContext;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? context?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action, Entity = entity, EntityId = entityId,
            IpAddress = context?.Connection.RemoteIpAddress?.ToString()
        });
    }
}

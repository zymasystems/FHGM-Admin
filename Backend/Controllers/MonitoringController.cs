using System.Diagnostics;
using System.Runtime.InteropServices;
using FHGM.Backend.Authentication;
using FHGM.Backend.Data;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/monitoring"), Authorize(Policy = Permissions.ViewMonitoring)]
public sealed class MonitoringController(AppDbContext db, HealthCheckService health, GalleryStorage storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(cancellationToken);
        using var process = Process.GetCurrentProcess();
        object? counts = null;
        if (report.Status == HealthStatus.Healthy)
        {
            counts = new
            {
                users = await db.Users.CountAsync(cancellationToken), activeUsers = await db.Users.CountAsync(x => x.IsActive, cancellationToken),
                blogPosts = await db.BlogPosts.CountAsync(cancellationToken), galleryImages = await db.GalleryImages.CountAsync(x => x.IsComplete && !x.IsDeleted, cancellationToken),
                events = await db.Events.CountAsync(cancellationToken), popups = await db.Popups.CountAsync(cancellationToken), auditRecords = await db.AuditLogs.CountAsync(cancellationToken)
            };
        }
        // Process values describe the backend container, not the entire VPS.
        return Ok(new
        {
            checkedAt = DateTimeOffset.UtcNow, status = report.Status.ToString(), runtime = RuntimeInformation.FrameworkDescription,
            uptimeSeconds = Math.Max(0, (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds),
            workingSetMb = Math.Round(process.WorkingSet64 / 1048576d, 1), managedMemoryMb = Math.Round(GC.GetTotalMemory(false) / 1048576d, 1),
            services = report.Entries.Select(x => new { name = x.Key, status = x.Value.Status.ToString(), durationMs = Math.Round(x.Value.Duration.TotalMilliseconds, 1) }),
            r2Configured = storage.IsConfigured,
            counts
        });
    }
}

using System.Security.Claims;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FHGM.Backend.Controllers;
[ApiController, Route("api/dashboard"), Authorize]
public sealed class DashboardSummaryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var admin = User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.ChurchAdmin);
        var blogTeam = admin || User.IsInRole(Roles.BlogEditor) || User.IsInRole(Roles.Writer);
        var media = admin || User.IsInRole(Roles.MediaManager);
        var communications = admin || User.IsInRole(Roles.CommunicationsManager);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var blogs = db.BlogPosts.AsNoTracking().Where(x => admin || User.IsInRole(Roles.BlogEditor) || x.AuthorId == userId);
        var now = DateTimeOffset.UtcNow;
        return Ok(new {
            drafts = blogTeam ? (int?)await blogs.CountAsync(x => x.Status == BlogStatus.Draft || x.Status == BlogStatus.ChangesRequested || x.Status == BlogStatus.Rejected) : null,
            awaitingReview = blogTeam ? (int?)await blogs.CountAsync(x => x.Status == BlogStatus.Submitted) : null,
            unpublishedPhotos = media ? (int?)await db.GalleryImages.CountAsync(x => x.IsComplete && !x.IsPublished && !x.IsDeleted) : null,
            incompleteUploads = media ? (int?)await db.GalleryImages.CountAsync(x => !x.IsComplete && !x.IsDeleted) : null,
            upcomingEvents = communications ? await db.Events.AsNoTracking().Where(x => x.EndsAt > now).OrderBy(x => x.StartsAt).Take(5).Select(x => new { x.Title, x.StartsAt, x.IsPublished }).ToListAsync() : null
        });
    }
}

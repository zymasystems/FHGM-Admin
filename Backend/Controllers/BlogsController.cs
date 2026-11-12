using System.Text.Json;
using FHGM.Backend.Authentication;
using FHGM.Backend.DTOs;
using FHGM.Backend.Repositories;
using FHGM.Backend.Entities;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/blog"), Authorize(Policy = Permissions.ManageBlogs)]
public sealed class BlogsController(IBlogRepository blogs, UserManager<AppUser> users, AuditService audit) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.ChurchAdmin);
    private bool IsEditor => User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.ChurchAdmin) || User.IsInRole(Roles.BlogEditor);
    private string UserId => users.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var query = blogs.Read().AsQueryable();
        if (!IsEditor) query = query.Where(x => x.AuthorId == UserId);
        return Ok(new { total = await query.CountAsync(), page,
            items = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * 25).Take(25)
                .Select(x => new { x.Id, x.Title, x.Slug, x.Status, x.Version, x.UpdatedAt, hasPendingChanges = x.PendingRevisionJson != null, author = x.Author.DisplayName }).ToListAsync() });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var post = await blogs.Read().FirstOrDefaultAsync(x => x.Id == id);
        if (post is null || (!IsEditor && post.AuthorId != UserId)) return NotFound();
        return Ok(await StaffPost(post));
    }

    [HttpPost]
    public async Task<IActionResult> Create(BlogRequest request)
    {
        var post = new BlogPost { AuthorId = UserId };
        Apply(post, request);
        blogs.Add(post);
        audit.Record("BlogCreated", "BlogPost", post.Id.ToString());
        await blogs.SaveAsync();
        return CreatedAtAction(nameof(Get), new { id = post.Id }, post);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, BlogRequest request)
    {
        var post = await blogs.FindAsync(id);
        if (post is null || (!IsEditor && post.AuthorId != UserId)) return NotFound();
        if (!IsAdmin && (!BlogWorkflow.CanEdit(post.Status) || (!IsEditor && post.Status == BlogStatus.Submitted)))
            return Conflict(new { message = "This article cannot be edited at its current stage. Archive and restore a published article before revising it." });
        if (post.Version != request.Version) return Conflict(new { message = "Reload this article; someone else changed it." });
        if (await blogs.Read().AnyAsync(x => x.Id != id && x.Slug == request.Slug.Trim()))
            return Conflict(new { message = "That article URL is already in use." });
        if (post.Status == BlogStatus.Published)
            post.PendingRevisionJson = JsonSerializer.Serialize(request);
        else { Apply(post, request); post.PendingRevisionJson = null; }
        post.Version++;
        audit.Record(post.Status == BlogStatus.Published ? "BlogRevisionSaved" : "BlogEdited", "BlogPost", post.Id.ToString());
        await blogs.SaveAsync();
        return Ok(await StaffPost(post));
    }

    [HttpPost("{id:guid}/workflow")]
    public async Task<IActionResult> Transition(Guid id, TransitionRequest request)
    {
        var post = await blogs.FindAsync(id);
        if (post is null || (!IsEditor && post.AuthorId != UserId)) return NotFound();
        if (!IsEditor && request.Action != "submit") return Forbid();
        if (post.Version != request.Version) return Conflict(new { message = "Reload this article; someone else changed it." });
        if (request.Action == "unpublish" && !IsAdmin) return Forbid();
        var directPublish = IsAdmin && request.Action == "publish" && (post.Status != BlogStatus.Published || post.PendingRevisionJson != null);
        if (IsAdmin && request.Action == "publish" && !request.Reviewed)
            return BadRequest(new { message = "Preview and review the saved article before publishing." });
        BlogStatus? next = directPublish ? BlogStatus.Published
            : IsAdmin && request.Action == "unpublish" && post.Status == BlogStatus.Published ? BlogStatus.Draft
            : IsAdmin && request.Action == "archive" && post.Status != BlogStatus.Archived ? BlogStatus.Archived
            : BlogWorkflow.Next(post.Status, request.Action);
        if (next is null) return Conflict(new { message = "That action is not allowed at the current stage." });
        if (request.Action is "request-changes" or "reject" && string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { message = "Provide a review note for the writer." });
        // A saved revision only becomes live on explicit publish. If hidden first,
        // carry the working content into that draft/archive without exposing it.
        if (post.PendingRevisionJson != null && (IsAdmin && request.Action == "publish" || next != BlogStatus.Published))
        {
            Apply(post, JsonSerializer.Deserialize<BlogRequest>(post.PendingRevisionJson)!);
            post.PendingRevisionJson = null;
        }
        post.Status = next.Value;
        post.ReviewNote = request.Note.Trim();
        post.UpdatedAt = DateTimeOffset.UtcNow;
        post.Version++;
        if (next == BlogStatus.Submitted) post.SubmittedAt = post.UpdatedAt;
        if (next == BlogStatus.Approved) post.ApprovedAt = post.UpdatedAt;
        if (next == BlogStatus.Published) post.PublishedAt = post.UpdatedAt;
        if (next == BlogStatus.Draft) { post.ApprovedAt = null; post.PublishedAt = null; }
        audit.Record("Blog:" + request.Action, "BlogPost", post.Id.ToString());
        await blogs.SaveAsync();
        return Ok(await StaffPost(post));
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.SuperAdmin + "," + Roles.ChurchAdmin)]
    public async Task<IActionResult> Delete(Guid id, int version)
    {
        var post = await blogs.FindAsync(id);
        if (post is null) return NotFound();
        if (post.Version != version) return Conflict(new { message = "Reload this article; someone else changed it." });
        blogs.Remove(post);
        audit.Record("BlogDeleted", "BlogPost", post.Id.ToString());
        await blogs.SaveAsync();
        return NoContent();
    }

    private async Task<object> StaffPost(BlogPost post)
    {
        var revision = post.PendingRevisionJson is null ? null : JsonSerializer.Deserialize<BlogRequest>(post.PendingRevisionJson);
        return new { post.Id, Title = revision?.Title.Trim() ?? post.Title, Slug = revision?.Slug.Trim() ?? post.Slug,
            Excerpt = revision?.Excerpt.Trim() ?? post.Excerpt, Content = revision?.Content.Trim() ?? post.Content,
            Category = revision?.Category.Trim() ?? post.Category, Tags = revision?.Tags.Trim() ?? post.Tags,
            SeoTitle = revision?.SeoTitle.Trim() ?? post.SeoTitle, SeoDescription = revision?.SeoDescription.Trim() ?? post.SeoDescription,
            FeaturedImageUrl = revision?.FeaturedImageUrl ?? post.FeaturedImageUrl, ContentFormat = revision?.ContentFormat ?? post.ContentFormat,
            post.AuthorId, post.Status, post.ReviewNote, post.Version, post.CreatedAt, post.UpdatedAt, post.SubmittedAt,
            post.ApprovedAt, post.PublishedAt, hasPendingChanges = revision != null,
            author = await users.Users.Where(x => x.Id == post.AuthorId).Select(x => x.DisplayName).FirstOrDefaultAsync() };
    }

    private static void Apply(BlogPost post, BlogRequest request)
    {
        post.FeaturedImageUrl = request.FeaturedImageUrl; post.ContentFormat = request.ContentFormat;
        post.Title = request.Title.Trim(); post.Slug = request.Slug.Trim();
        post.Excerpt = request.Excerpt.Trim(); post.Content = request.Content.Trim();
        post.Category = request.Category.Trim(); post.Tags = request.Tags.Trim();
        post.SeoTitle = request.SeoTitle.Trim(); post.SeoDescription = request.SeoDescription.Trim();
        post.UpdatedAt = DateTimeOffset.UtcNow;
    }
}


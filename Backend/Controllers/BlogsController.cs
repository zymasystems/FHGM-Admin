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
    private bool IsEditor => User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.BlogEditor);
    private string UserId => users.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var query = blogs.Read().AsQueryable();
        if (!IsEditor) query = query.Where(x => x.AuthorId == UserId);
        return Ok(new { total = await query.CountAsync(), page,
            items = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * 25).Take(25)
                .Select(x => new { x.Id, x.Title, x.Slug, x.Status, x.Version, x.UpdatedAt, author = x.Author.DisplayName }).ToListAsync() });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var post = await blogs.Read().FirstOrDefaultAsync(x => x.Id == id);
        if (post is null || (!IsEditor && post.AuthorId != UserId)) return NotFound();
        return Ok(post);
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
        if (!BlogWorkflow.CanEdit(post.Status) || (!IsEditor && post.Status == BlogStatus.Submitted))
            return Conflict(new { message = "This article cannot be edited at its current stage. Archive and restore a published article before revising it." });
        if (post.Version != request.Version) return Conflict(new { message = "Reload this article; someone else changed it." });
        Apply(post, request);
        post.Version++;
        audit.Record("BlogEdited", "BlogPost", post.Id.ToString());
        await blogs.SaveAsync();
        return Ok(post);
    }

    [HttpPost("{id:guid}/workflow")]
    public async Task<IActionResult> Transition(Guid id, TransitionRequest request)
    {
        var post = await blogs.FindAsync(id);
        if (post is null || (!IsEditor && post.AuthorId != UserId)) return NotFound();
        if (!IsEditor && request.Action != "submit") return Forbid();
        if (post.Version != request.Version) return Conflict(new { message = "Reload this article; someone else changed it." });
        var next = BlogWorkflow.Next(post.Status, request.Action);
        if (next is null) return Conflict(new { message = "That action is not allowed at the current stage." });
        if (request.Action is "request-changes" or "reject" && string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { message = "Provide a review note for the writer." });
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
        return Ok(post);
    }

    private static void Apply(BlogPost post, BlogRequest request)
    {
        post.Title = request.Title.Trim(); post.Slug = request.Slug.Trim();
        post.Excerpt = request.Excerpt.Trim(); post.Content = request.Content.Trim();
        post.Category = request.Category.Trim(); post.Tags = request.Tags.Trim();
        post.SeoTitle = request.SeoTitle.Trim(); post.SeoDescription = request.SeoDescription.Trim();
        post.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

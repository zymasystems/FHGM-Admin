using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FHGM.Admin.Models;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Writer = "Writer";
    public const string BlogEditor = "BlogEditor";
    public const string MediaManager = "MediaManager";
    public const string CommunicationsManager = "CommunicationsManager";
    public const string BlogTeam = SuperAdmin + "," + Writer + "," + BlogEditor;
    public const string Editors = SuperAdmin + "," + BlogEditor;
    public static readonly string[] All = [SuperAdmin, Writer, BlogEditor, MediaManager, CommunicationsManager];
}

public sealed class AppUser : IdentityUser
{
    [MaxLength(120)] public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public enum BlogStatus { Draft, Submitted, ChangesRequested, Rejected, Approved, Published, Archived }

public sealed class BlogPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(180)] public string Title { get; set; } = "";
    [MaxLength(200)] public string Slug { get; set; } = "";
    [MaxLength(500)] public string Excerpt { get; set; } = "";
    [MaxLength(50000)] public string Content { get; set; } = "";
    [MaxLength(100)] public string Category { get; set; } = "";
    [MaxLength(500)] public string Tags { get; set; } = "";
    [MaxLength(180)] public string SeoTitle { get; set; } = "";
    [MaxLength(320)] public string SeoDescription { get; set; } = "";
    public string AuthorId { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public AppUser Author { get; set; } = null!;
    public BlogStatus Status { get; set; }
    [MaxLength(2000)] public string ReviewNote { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    [MaxLength(120)] public string Action { get; set; } = "";
    [MaxLength(120)] public string Entity { get; set; } = "";
    [MaxLength(200)] public string EntityId { get; set; } = "";
    [MaxLength(64)] public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

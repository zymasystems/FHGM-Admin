using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Authorization;

namespace FHGM.Backend.Authentication;

public static class Permissions
{
    public const string ManageBlogs = "content.blog.manage";
    public const string ReviewBlogs = "content.blog.review";
    public const string ManageUsers = "accounts.manage";
    public const string ViewAudit = "audit.read";
    public const string ManageGallery = "gallery.manage";
    public const string ManageEvents = "events.manage";
    public const string ManagePopups = "popups.manage";
    public const string ViewMonitoring = "monitoring.read";
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(ManageBlogs, p => p.RequireRole(Roles.SuperAdmin, Roles.ChurchAdmin, Roles.Writer, Roles.BlogEditor));
        options.AddPolicy(ReviewBlogs, p => p.RequireRole(Roles.SuperAdmin, Roles.ChurchAdmin, Roles.BlogEditor));
        options.AddPolicy(ManageUsers, p => p.RequireRole(Roles.SuperAdmin));
        options.AddPolicy(ViewAudit, p => p.RequireRole(Roles.SuperAdmin));
        options.AddPolicy(ManageGallery, p => p.RequireRole(Roles.SuperAdmin, Roles.ChurchAdmin, Roles.MediaManager));
        options.AddPolicy(ManageEvents, p => p.RequireRole(Roles.SuperAdmin, Roles.ChurchAdmin, Roles.CommunicationsManager));
        options.AddPolicy(ManagePopups, p => p.RequireRole(Roles.SuperAdmin, Roles.ChurchAdmin, Roles.CommunicationsManager));
        options.AddPolicy(ViewMonitoring, p => p.RequireRole(Roles.SuperAdmin));
    }
}

using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Authorization;

namespace FHGM.Backend.Authentication;

public static class Permissions
{
    public const string ManageBlogs = "content.blog.manage";
    public const string ReviewBlogs = "content.blog.review";
    public const string ManageUsers = "accounts.manage";
    public const string ViewAudit = "audit.read";
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(ManageBlogs, p => p.RequireRole(Roles.SuperAdmin, Roles.Writer, Roles.BlogEditor));
        options.AddPolicy(ReviewBlogs, p => p.RequireRole(Roles.SuperAdmin, Roles.BlogEditor));
        options.AddPolicy(ManageUsers, p => p.RequireRole(Roles.SuperAdmin));
        options.AddPolicy(ViewAudit, p => p.RequireRole(Roles.SuperAdmin));
    }
}

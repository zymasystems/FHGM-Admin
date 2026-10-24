using FHGM.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FHGM.Admin.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    [HttpGet("/"), HttpGet("/admin/")]
    public IActionResult Index() => View(new DashboardViewModel("overview"));
    [Authorize(Policy = "ManageBlogs"), HttpGet("/admin/blogs")]
    public IActionResult Blogs() => View("Index", new DashboardViewModel("blogs"));
    [Authorize(Policy = "ManageGallery"), HttpGet("/admin/gallery")]
    public IActionResult Gallery() => View("Index", new DashboardViewModel("gallery"));
    [Authorize(Policy = "ManageUsers"), HttpGet("/admin/users")]
    public IActionResult Users() => View("Index", new DashboardViewModel("users"));
    [Authorize(Policy = "ViewAudit"), HttpGet("/admin/audit")]
    public IActionResult Audit() => View("Index", new DashboardViewModel("audit"));
    [Authorize(Policy = "ManageEvents"), HttpGet("/admin/events")]
    public IActionResult Events() => View("Index", new DashboardViewModel("events"));
    [Authorize(Policy = "ManagePopups"), HttpGet("/admin/popups")]
    public IActionResult Popups() => View("Index", new DashboardViewModel("popups"));
    [Authorize(Policy = "ViewMonitoring"), HttpGet("/admin/monitoring")]
    public IActionResult Monitoring() => View("Index", new DashboardViewModel("monitoring"));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FHGM.Admin.Controllers;

[AllowAnonymous]
public sealed class PublicController : Controller
{
    // A preview of the CMS feed. The existing ministry site remains the visitor-facing website.
    [HttpGet("/public/")]
    public IActionResult Index() => View();
    [HttpGet("/public/gallery")]
    public IActionResult Gallery() => View();
}

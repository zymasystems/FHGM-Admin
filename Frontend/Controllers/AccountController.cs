using FHGM.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FHGM.Admin.Controllers;

public sealed class AccountController : Controller
{
    [AllowAnonymous, HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl = null) => User.Identity?.IsAuthenticated == true
        ? LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/")
        : View(new LoginViewModel(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/"));
}

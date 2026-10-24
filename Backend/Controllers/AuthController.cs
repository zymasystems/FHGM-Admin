using FHGM.Backend.DTOs;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(SignInManager<AppUser> signIn, UserManager<AppUser> users,
    IAntiforgery antiforgery, AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive) return Unauthorized(new { message = "Sign-in failed. Check your credentials or contact your administrator." });
        var result = await signIn.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized(new { message = "Sign-in failed. Check your credentials or contact your administrator." });
        audit.Record("Login", "User", user.Id, user.Id);
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Email, user.DisplayName, roles = await users.GetRolesAsync(user) });
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var user = (await users.GetUserAsync(User))!;
        return Ok(new { user.Id, user.Email, user.DisplayName, roles = await users.GetRolesAsync(user) });
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout()
    {
        audit.Record("Logout", "User", users.GetUserId(User)!);
        await db.SaveChangesAsync();
        await signIn.SignOutAsync();
        return NoContent();
    }
}

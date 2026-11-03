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
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized(new { message = "Sign-in failed. Check your credentials or contact your administrator." });
        if (await users.GetTwoFactorEnabledAsync(user))
        {
            var valid = !string.IsNullOrWhiteSpace(request.RecoveryCode)
                ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, request.RecoveryCode.Trim())).Succeeded
                : await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.TwoFactorCode.Replace(" ", ""));
            if (!valid) { await users.AccessFailedAsync(user); return Unauthorized(new { message = "Sign-in failed. Check your credentials and authenticator or recovery code." }); }
        }
        await users.ResetAccessFailedCountAsync(user);
        await signIn.SignInAsync(user, false);
        audit.Record("Login", "User", user.Id, user.Id);
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Email, user.DisplayName, user.MustChangePassword, roles = await users.GetRolesAsync(user) });
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var user = (await users.GetUserAsync(User))!;
        return Ok(new { user.Id, user.Email, user.DisplayName, user.MustChangePassword, roles = await users.GetRolesAsync(user) });
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


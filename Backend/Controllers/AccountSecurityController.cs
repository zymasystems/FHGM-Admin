using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/security"), Authorize, EnableRateLimiting("account")]
public sealed class AccountSecurityController(UserManager<AppUser> users, SignInManager<AppUser> signIn,
    AppDbContext db, AuditService audit, IConfiguration config, ILogger<AccountSecurityController> logger) : ControllerBase
{
    public sealed record PasswordChange([Required] string CurrentPassword, [Required, MinLength(12), MaxLength(128)] string NewPassword);
    public sealed record PasswordProof([Required] string Password, string Code = "");
    public sealed record Forgot([Required, EmailAddress] string Email);
    public sealed record Reset([Required, EmailAddress] string Email, [Required] string Token, [Required, MinLength(12), MaxLength(128)] string NewPassword);
    private IActionResult Invalid() => BadRequest(new { message = "Could not verify these details. Check your password and verification code." });
    private async Task<bool> PasswordValid(AppUser user, string password)
    {
        if (await users.IsLockedOutAsync(user)) return false;
        if (await users.CheckPasswordAsync(user, password)) return true;
        await users.AccessFailedAsync(user); return false;
    }
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var user = (await users.GetUserAsync(User))!;
        return Ok(new { user.MustChangePassword, twoFactorEnabled = await users.GetTwoFactorEnabledAsync(user), recoveryCodesRemaining = await users.CountRecoveryCodesAsync(user) });
    }
    [HttpPost("password")]
    public async Task<IActionResult> Change(PasswordChange request)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!await PasswordValid(user, request.CurrentPassword)) return Invalid();
        if (request.NewPassword == request.CurrentPassword) return BadRequest(new { message = "Choose a different password." });
        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
        user.MustChangePassword = false;
        if (!(await users.UpdateAsync(user)).Succeeded) return Conflict();
        audit.Record("PasswordChanged", "User", user.Id); await db.SaveChangesAsync(); await tx.CommitAsync();
        await signIn.RefreshSignInAsync(user);
        return NoContent();
    }
    [HttpPost("mfa/setup")]
    public async Task<IActionResult> Setup(PasswordProof request)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!await PasswordValid(user, request.Password)) return Invalid();
        if (await users.GetTwoFactorEnabledAsync(user)) return Conflict(new { message = "Authenticator is already enabled." });
        if (await users.GetAuthenticatorKeyAsync(user) is null && !(await users.ResetAuthenticatorKeyAsync(user)).Succeeded) return Conflict();
        return Ok(new { key = await users.GetAuthenticatorKeyAsync(user), issuer = "FHGM", account = user.Email });
    }
    [HttpPost("mfa/enable")]
    public async Task<IActionResult> Enable(PasswordProof request)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!await PasswordValid(user, request.Password)) return Invalid();
        if (await users.GetTwoFactorEnabledAsync(user)) return Conflict();
        if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.Code.Replace(" ", "")))
        { await users.AccessFailedAsync(user); return Invalid(); }
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!(await users.SetTwoFactorEnabledAsync(user, true)).Succeeded) return Conflict();
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await users.UpdateSecurityStampAsync(user);
        audit.Record("MfaEnabled", "User", user.Id); await db.SaveChangesAsync(); await tx.CommitAsync();
        await signIn.RefreshSignInAsync(user); return Ok(new { recoveryCodes = codes });
    }
    [HttpPost("mfa/disable")]
    public async Task<IActionResult> Disable(PasswordProof request)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!await PasswordValid(user, request.Password)) return Invalid();
        if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.Code.Replace(" ", "")))
        { await users.AccessFailedAsync(user); return Invalid(); }
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!(await users.SetTwoFactorEnabledAsync(user, false)).Succeeded) return Conflict();
        await users.ResetAuthenticatorKeyAsync(user); await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
        await users.UpdateSecurityStampAsync(user);
        audit.Record("MfaDisabled", "User", user.Id); await db.SaveChangesAsync(); await tx.CommitAsync();
        await signIn.RefreshSignInAsync(user); return NoContent();
    }
    [HttpPost("forgot"), AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(Forgot request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is { IsActive: true, EmailConfirmed: true } && !string.IsNullOrWhiteSpace(config["Email:Host"]))
        {
            try
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var url = "https://admin.hisgraceministries.co.za/account/recovery#email=" + Uri.EscapeDataString(request.Email) + "&token=" + Uri.EscapeDataString(token);
                using var message = new MailMessage(config["Email:From"]!, request.Email, "Reset your FHGM password", "Open this link to choose a new password:\n" + url + "\nIf you did not request this, ignore this email. Your authenticator remains required.");
                using var smtp = new SmtpClient(config["Email:Host"], config.GetValue("Email:Port", 587))
                { EnableSsl = true, Credentials = new NetworkCredential(config["Email:Username"], config["Email:Password"]), Timeout = 15000 };
                await smtp.SendMailAsync(message);
            }
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException or ArgumentException)
            { logger.LogWarning("Password reset email could not be delivered. Check SMTP configuration."); }
        }
        return Ok(new { message = "If this is an active account and email delivery is configured, a reset link will arrive shortly." });
    }
    [HttpPost("reset"), AllowAnonymous]
    public async Task<IActionResult> ResetPassword(Reset request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not { IsActive: true }) return Invalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = "The link is invalid/expired, or the password does not meet the requirements. Request a new link and use 12+ characters with upper/lowercase, a number and a symbol." });
        user.MustChangePassword = false;
        if (!(await users.UpdateAsync(user)).Succeeded) return Conflict();
        audit.Record("PasswordReset", "User", user.Id); await db.SaveChangesAsync(); await tx.CommitAsync();
        return NoContent();
    }
}

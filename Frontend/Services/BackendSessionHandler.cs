using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FHGM.Admin.Services;

public sealed class BackendSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, BackendClient backend) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // API requests are authorized by the backend itself; only Razor pages need a frontend principal.
        if (Request.Path.StartsWithSegments("/api") || Request.Path.StartsWithSegments("/health"))
            return AuthenticateResult.NoResult();
        try
        {
            var session = await backend.SessionAsync(Context);
            if (session is null) return AuthenticateResult.NoResult();
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, session.Id), new(ClaimTypes.Name, session.DisplayName), new(ClaimTypes.Email, session.Email) };
            claims.AddRange(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Logger.LogWarning("Backend session validation is unavailable.");
            Context.Items["BackendUnavailable"] = true;
            return AuthenticateResult.Fail("Session validation unavailable.");
        }
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Context.Items.ContainsKey("BackendUnavailable")) { Response.StatusCode = 503; return Task.CompletedTask; }
        Response.Redirect("/account/login?returnUrl=" + Uri.EscapeDataString(Request.PathBase + Request.Path + Request.QueryString));
        return Task.CompletedTask;
    }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    { Response.StatusCode = 403; return Task.CompletedTask; }
}

using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Identity;

namespace FHGM.Backend.Authentication;

public static class IdentityConfiguration
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, bool isDevelopment)
    {
        services.AddIdentity<AppUser, IdentityRole>(o =>
        {
            o.Password.RequiredLength = 12;
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "FHGM.Session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
            o.Events.OnValidatePrincipal = async c =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(c);
                if (c.Principal is null) return;
                var manager = c.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = await manager.GetUserAsync(c.Principal);
                if (user is null || !user.IsActive)
                {
                    c.RejectPrincipal();
                    await c.HttpContext.RequestServices.GetRequiredService<SignInManager<AppUser>>().SignOutAsync();
                }
            };
        });
        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-TOKEN";
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddAuthorization(Permissions.Configure);
        return services;
    }
}

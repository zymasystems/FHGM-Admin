using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FHGM.Admin.Data;
using FHGM.Admin.Models;
using FHGM.Admin.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Where(x => x != "--initialize").ToArray(),
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});
builder.Services.AddProblemDetails();
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings__Default is required.")));
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 12;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "FHGM.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
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
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().SetApplicationName("FHGM").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
if (args.Contains("--initialize"))
{
    await Bootstrap.RunAsync(app.Services, app.Configuration);
    return;
}
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (DbUpdateConcurrencyException)
    { await Results.Problem(statusCode: 409, title: "This record changed. Reload it before saving.").ExecuteAsync(context); }
    catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
    { await Results.Problem(statusCode: 409, title: "This slug or email already exists.").ExecuteAsync(context); }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/public/"));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (AppDbContext db) =>
{
    try
    {
        await db.BlogPosts.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync();
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception ex) when (ex is Npgsql.NpgsqlException or TimeoutException)
    { return Results.StatusCode(503); }
});
app.Run();

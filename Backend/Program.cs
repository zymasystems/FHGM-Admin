using FHGM.Backend.Authentication;
using FHGM.Backend.Infrastructure;
using FHGM.Backend.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args.Where(x => x != "--initialize").ToArray());
builder.Services.AddProblemDetails();
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings__Default is required.")));
builder.Services.AddIdentityAuthentication(builder.Environment.IsDevelopment());
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().SetApplicationName("FHGM").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<IBlogRepository, BlogRepository>();
builder.Services.Configure<R2Options>(builder.Configuration.GetSection("R2"));
builder.Services.AddScoped<GalleryStorage>();
builder.Services.AddHostedService<GalleryCleanupService>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (IPAddress.TryParse(builder.Configuration["Proxy:TrustedAddress"], out var address)) options.KnownProxies.Add(address);
});
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
app.UseForwardedHeaders();
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
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.Run();

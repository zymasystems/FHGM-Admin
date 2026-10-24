using System.Net;
using FHGM.Admin.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddControllersWithViews();
var backendUrl = builder.Configuration["Backend:BaseUrl"] ?? "http://127.0.0.1:5081/";
if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var backendUri) || backendUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(backendUri.UserInfo))
    throw new InvalidOperationException("Backend__BaseUrl must be an HTTP(S) URL.");
builder.Services.AddHttpClient("backend", client => { client.BaseAddress = backendUri; client.Timeout = TimeSpan.FromSeconds(20); })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false });
builder.Services.AddTransient<BackendClient>(services => new BackendClient(services.GetRequiredService<IHttpClientFactory>().CreateClient("backend")));
builder.Services.AddAuthentication("BackendSession").AddScheme<AuthenticationSchemeOptions, BackendSessionHandler>("BackendSession", _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageBlogs", policy => policy.RequireRole("SuperAdmin", "Writer", "BlogEditor"));
    options.AddPolicy("ManageUsers", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("ViewAudit", policy => policy.RequireRole("SuperAdmin"));
});
builder.Services.AddHealthChecks().AddCheck<BackendHealthCheck>("backend", tags: ["ready"]);
// Set only when the peer is a known reverse proxy. Do not trust arbitrary forwarded headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (IPAddress.TryParse(builder.Configuration["Proxy:TrustedAddress"], out var address)) options.KnownProxies.Add(address);
});
var app = builder.Build();
app.UseExceptionHandler();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    context.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !context.RequestAborted.IsCancellationRequested)
    { if (!context.Response.HasStarted) await Results.Problem(statusCode: 503, title: "The content service is unavailable. Please try again shortly.").ExecuteAsync(context); }
});
app.UseStaticFiles();
app.UseRouting();
app.Map("/api/{**path}", async (HttpContext context, BackendClient backend) => await backend.RelayAsync(context));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.Run();

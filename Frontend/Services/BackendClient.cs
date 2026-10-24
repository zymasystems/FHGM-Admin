using System.Net;
using System.Net.Http.Json;
using FHGM.Admin.ViewModels;

namespace FHGM.Admin.Services;

// Uses the backend's Identity session. There are no user accounts, password hashes or DB connection in MVC.
public sealed class BackendClient(HttpClient http)
{
    public async Task<SessionViewModel?> SessionAsync(HttpContext context)
    {
        if (!context.Request.Cookies.ContainsKey("FHGM.Session")) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToString());
        using var response = await http.SendAsync(request, context.RequestAborted);
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            foreach (var cookie in cookies) context.Response.Headers.Append("Set-Cookie", cookie);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionViewModel>(context.RequestAborted);
    }

    public async Task RelayAsync(HttpContext context)
    {
        // Only /api/ routes on the configured private backend are reachable, never a caller-supplied host.
        var path = context.Request.Path.Value!;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) { context.Response.StatusCode = 404; return; }
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path.TrimStart('/') + context.Request.QueryString);
        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType is { } contentType)
                request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }
        foreach (var name in new[] { "Cookie", "X-CSRF-TOKEN", "Accept" })
            if (context.Request.Headers.TryGetValue(name, out var values)) request.Headers.TryAddWithoutValidation(name, values.ToArray());
        // Use only the socket peer address. The host Nginx proxy is explicitly trusted in Program.cs.
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", context.Connection.RemoteIpAddress?.ToString());
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            foreach (var cookie in cookies) context.Response.Headers.Append("Set-Cookie", cookie);
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
        context.Response.Headers.CacheControl = "no-store";
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}

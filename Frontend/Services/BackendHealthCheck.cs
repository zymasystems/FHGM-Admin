using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FHGM.Admin.Services;

public sealed class BackendHealthCheck(IHttpClientFactory clients) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await clients.CreateClient("backend").GetAsync("health/ready", cancellationToken);
            return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Backend is not ready.");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("Backend is unavailable.", ex); }
    }
}

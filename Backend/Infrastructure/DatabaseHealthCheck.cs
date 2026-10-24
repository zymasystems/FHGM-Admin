using FHGM.Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FHGM.Backend.Infrastructure;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.BlogPosts.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await db.Events.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await db.Popups.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("Application database is unavailable.", ex); }
    }
}

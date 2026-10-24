using FHGM.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Services;

public sealed class GalleryCleanupService(IServiceScopeFactory scopes, ILogger<GalleryCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var storage = scope.ServiceProvider.GetRequiredService<GalleryStorage>();
                if (!storage.IsConfigured) continue;
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
                var now = DateTimeOffset.UtcNow;
                var images = await db.GalleryImages.Where(x => x.IsDeleted && x.UploadExpiresAt < now || !x.IsComplete && x.UploadExpiresAt < cutoff
                    || x.IsComplete && !x.IsDeleted && !x.StagingCleaned && x.UploadExpiresAt < now).Take(50).ToListAsync(stoppingToken);
                foreach (var image in images)
                {
                    try
                    {
                        if (image.IsComplete && !image.IsDeleted) { await storage.DeleteStagingAsync(image, stoppingToken); image.StagingCleaned = true; }
                        else { await storage.DeleteAsync(image, stoppingToken); db.GalleryImages.Remove(image); }
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Storage cleanup will retry image {ImageId}.", image.Id); }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Gallery cleanup is temporarily unavailable."); }
        }
    }
}

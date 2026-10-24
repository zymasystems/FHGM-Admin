using System.Net;
using System.Security.Claims;
using Amazon.S3;
using FHGM.Backend.Authentication;
using FHGM.Backend.Data;
using FHGM.Backend.DTOs;
using FHGM.Backend.Entities;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/gallery"), Authorize(Policy = Permissions.ManageGallery)]
public sealed class GalleryController(AppDbContext db, GalleryStorage storage, AuditService audit) : ControllerBase
{
    [HttpGet("storage")]
    public IActionResult Storage() => Ok(new { configured = storage.IsConfigured, maximumFileSize = 10485760, types = new[] { "image/jpeg", "image/png", "image/webp" } });

    [HttpGet("albums")]
    public async Task<IActionResult> Albums(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        return Ok(new { total = await db.GalleryAlbums.CountAsync(), page, items = await db.GalleryAlbums.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Skip((page - 1) * 50).Take(50)
            .Select(x => new { x.Id, x.Title, x.Slug, x.Description, x.IsPublished, x.Version,
                imageCount = x.Images.Count(i => i.IsComplete && !i.IsDeleted) }).ToListAsync() });
    }

    [HttpPost("albums")]
    public async Task<IActionResult> CreateAlbum(AlbumRequest request)
    {
        var album = new GalleryAlbum { Title = request.Title.Trim(), Slug = request.Slug, Description = request.Description.Trim(), IsPublished = request.IsPublished };
        db.GalleryAlbums.Add(album);
        audit.Record("AlbumCreated", "GalleryAlbum", album.Id.ToString());
        await db.SaveChangesAsync();
        return StatusCode(201, new { album.Id, album.Title, album.Slug, album.Description, album.IsPublished, album.Version });
    }

    [HttpPut("albums/{id:guid}")]
    public async Task<IActionResult> UpdateAlbum(Guid id, AlbumRequest request)
    {
        var album = await db.GalleryAlbums.FindAsync(id);
        if (album is null) return NotFound();
        if (album.Version != request.Version) return Conflict(new { message = "Reload the album; it changed since you opened it." });
        album.Title = request.Title.Trim(); album.Slug = request.Slug; album.Description = request.Description.Trim();
        album.IsPublished = request.IsPublished; album.Version++;
        audit.Record(album.IsPublished ? "AlbumPublishedOrEdited" : "AlbumUnpublishedOrEdited", "GalleryAlbum", id.ToString());
        await db.SaveChangesAsync();
        return Ok(new { album.Id, album.Title, album.Slug, album.Description, album.IsPublished, album.Version });
    }

    [HttpGet("albums/{id:guid}/images")]
    public async Task<IActionResult> Images(Guid id, int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        if (!await db.GalleryAlbums.AnyAsync(x => x.Id == id)) return NotFound();
        var query = db.GalleryImages.AsNoTracking().Where(x => x.AlbumId == id && !x.IsDeleted);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * 24).Take(24)
            .Select(x => new { x.Id, x.FileName, x.AltText, x.Caption, x.Size, x.IsComplete, x.IsPublished, x.Version, x.StorageKey }).ToListAsync();
        return Ok(new { total = await query.CountAsync(), page, items = items.Select(x => new
        { x.Id, x.FileName, x.AltText, x.Caption, x.Size, x.IsComplete, x.IsPublished, x.Version, url = x.IsComplete ? storage.PublicUrl(x.StorageKey) : null }) });
    }

    [HttpPost("uploads")]
    public async Task<IActionResult> BeginUpload(ImageUploadRequest request)
    {
        if (!storage.IsConfigured) return StatusCode(503, new { message = "Gallery storage is not configured. Your administrator needs to connect Cloudflare R2." });
        var expectedExtension = request.ContentType switch
        { "image/jpeg" => new[] { ".jpg", ".jpeg" }, "image/png" => [".png"], "image/webp" => [".webp"], _ => [] };
        if (!expectedExtension.Contains(Path.GetExtension(request.FileName).ToLowerInvariant()))
            return BadRequest(new { message = "Choose a JPEG, PNG or WebP image with a matching file extension." });
        if (!await db.GalleryAlbums.AnyAsync(x => x.Id == request.AlbumId)) return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var recent = DateTimeOffset.UtcNow.AddHours(-1);
        if (await db.GalleryImages.CountAsync(x => x.UploadedBy == userId && !x.IsComplete && !x.IsDeleted && x.CreatedAt > recent) >= 20)
            return StatusCode(429, new { message = "Finish or remove your pending uploads before adding more images." });
        var image = new GalleryImage
        {
            AlbumId = request.AlbumId, FileName = Path.GetFileName(request.FileName), ContentType = request.ContentType,
            Size = request.Size, AltText = request.AltText.Trim(), UploadedBy = userId, UploadExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        image.StorageKey = $"gallery/{DateTimeOffset.UtcNow:yyyy/MM}/{image.Id:N}{expectedExtension[0]}";
        image.StagingKey = "staging/" + image.StorageKey;
        var uploadUrl = await storage.UploadUrlAsync(image);
        db.GalleryImages.Add(image);
        audit.Record("ImageUploadAuthorized", "GalleryImage", image.Id.ToString());
        await db.SaveChangesAsync();
        return StatusCode(201, new { image.Id, uploadUrl, image.UploadExpiresAt, image.ContentType });
    }

    [HttpPost("images/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var image = await db.GalleryImages.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (image is null) return NotFound();
        if (image.UploadedBy != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole(Roles.SuperAdmin)) return Forbid();
        if (image.IsComplete) return Ok(new { image.Id, image.Version });
        if (DateTimeOffset.UtcNow > image.UploadExpiresAt.AddMinutes(5)) return Conflict(new { message = "This upload has expired. Remove it and choose the image again." });
        if (!storage.IsConfigured) return StatusCode(503, new { message = "Gallery storage is not configured." });
        try { await storage.VerifyAndPromoteAsync(image, cancellationToken); }
        catch (InvalidDataException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        { return Conflict(new { message = "The upload is missing or changed. Upload the image again before completing it." }); }
        catch (AmazonS3Exception) { return StatusCode(503, new { message = "Image storage could not verify this upload. Check R2 configuration and retry." }); }
        image.IsComplete = true; image.Version++;
        audit.Record("ImageUploadCompleted", "GalleryImage", id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { image.Id, image.Version });
    }

    [HttpPut("images/{id:guid}")]
    public async Task<IActionResult> UpdateImage(Guid id, ImageUpdateRequest request)
    {
        var image = await db.GalleryImages.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (image is null) return NotFound();
        if (!image.IsComplete) return Conflict(new { message = "Complete the upload before publishing it." });
        if (image.Version != request.Version) return Conflict(new { message = "Reload this album; the image changed." });
        image.AltText = request.AltText.Trim(); image.Caption = request.Caption.Trim(); image.IsPublished = request.IsPublished; image.Version++;
        audit.Record(image.IsPublished ? "ImagePublishedOrEdited" : "ImageUnpublishedOrEdited", "GalleryImage", id.ToString());
        await db.SaveChangesAsync();
        return Ok(new { image.Id, image.AltText, image.Caption, image.IsPublished, image.Version });
    }

    [HttpDelete("images/{id:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var image = await db.GalleryImages.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (image is null) return NotFound();
        image.IsDeleted = true; image.IsPublished = false; image.Version++;
        audit.Record("ImageRemoved", "GalleryImage", id.ToString());
        await db.SaveChangesAsync();
        // Hidden immediately; cleanup retries R2 deletions in the background without making the image visible again.
        return Accepted(new { message = "Image removed from the gallery. Storage cleanup will run shortly." });
    }
}

using FHGM.Backend.Data;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/public/gallery"), AllowAnonymous]
public sealed class PublicGalleryController(AppDbContext db, GalleryStorage storage) : ControllerBase
{
    [HttpGet("albums")]
    public async Task<IActionResult> Albums(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var query = db.GalleryAlbums.AsNoTracking().Where(x => x.IsPublished && x.Images.Any(i => i.IsComplete && i.IsPublished && !i.IsDeleted));
        var albums = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * 20).Take(20)
            .Select(x => new { x.Slug, x.Title, x.Description, coverKey = x.Images.Where(i => i.IsComplete && i.IsPublished && !i.IsDeleted)
                .OrderByDescending(i => i.CreatedAt).Select(i => i.StorageKey).First(), imageCount = x.Images.Count(i => i.IsComplete && i.IsPublished && !i.IsDeleted) }).ToListAsync();
        return Ok(new { total = await query.CountAsync(), page, items = albums.Select(x => new { x.Slug, x.Title, x.Description, coverUrl = storage.PublicUrl(x.coverKey), x.imageCount }) });
    }

    [HttpGet("albums/{slug}")]
    public async Task<IActionResult> Album(string slug, int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var album = await db.GalleryAlbums.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug && x.IsPublished);
        if (album is null) return NotFound();
        var query = db.GalleryImages.AsNoTracking().Where(x => x.AlbumId == album.Id && x.IsComplete && x.IsPublished && !x.IsDeleted);
        var images = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * 24).Take(24)
            .Select(x => new { x.AltText, x.Caption, x.StorageKey }).ToListAsync();
        return Ok(new { album.Title, album.Description, total = await query.CountAsync(), page,
            items = images.Select(x => new { x.AltText, x.Caption, url = storage.PublicUrl(x.StorageKey) }) });
    }
}

using FHGM.Backend.Repositories;
using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/public/blog"), AllowAnonymous]
public sealed class PublicController(IBlogRepository blogs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var query = blogs.Read().Where(x => x.Status == BlogStatus.Published);
        return Ok(new { total = await query.CountAsync(), page,
            items = await query.OrderByDescending(x => x.PublishedAt).Skip((page - 1) * 12).Take(12)
                .Select(x => new { x.Slug, x.Title, x.Excerpt, x.Category, x.PublishedAt, author = x.Author.DisplayName }).ToListAsync() });
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug)
    {
        var post = await blogs.Read().Where(x => x.Status == BlogStatus.Published && x.Slug == slug)
            .Select(x => new { x.Slug, x.Title, x.Excerpt, x.Content, x.Category, x.Tags, x.SeoTitle, x.SeoDescription,
                x.PublishedAt, author = x.Author.DisplayName }).FirstOrDefaultAsync();
        return post is null ? NotFound() : Ok(post);
    }
}

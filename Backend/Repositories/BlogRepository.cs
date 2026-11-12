using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Repositories;

public interface IBlogRepository
{
    IQueryable<BlogPost> Read();
    Task<BlogPost?> FindAsync(Guid id);
    void Add(BlogPost post);
    void Remove(BlogPost post);
    Task SaveAsync();
}

public sealed class BlogRepository(AppDbContext db) : IBlogRepository
{
    public IQueryable<BlogPost> Read() => db.BlogPosts.AsNoTracking();
    public Task<BlogPost?> FindAsync(Guid id) => db.BlogPosts.FindAsync(id).AsTask();
    public void Add(BlogPost post) => db.BlogPosts.Add(post);
    public void Remove(BlogPost post) => db.BlogPosts.Remove(post);
    public async Task SaveAsync() => await db.SaveChangesAsync();
}

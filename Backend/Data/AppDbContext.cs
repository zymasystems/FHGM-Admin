using FHGM.Backend.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<GalleryAlbum> GalleryAlbums => Set<GalleryAlbum>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<BlogPost>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<BlogPost>().Property(x => x.Version).IsConcurrencyToken();
        builder.Entity<BlogPost>().HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AuditLog>().HasIndex(x => x.CreatedAt);
        builder.Entity<GalleryAlbum>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<GalleryAlbum>().Property(x => x.Version).IsConcurrencyToken();
        builder.Entity<GalleryImage>().Property(x => x.Version).IsConcurrencyToken();
        builder.Entity<GalleryImage>().HasIndex(x => x.StorageKey).IsUnique();
        builder.Entity<GalleryImage>().HasIndex(x => new { x.AlbumId, x.IsComplete, x.IsPublished, x.IsDeleted });
        builder.Entity<GalleryImage>().HasOne(x => x.Album).WithMany(x => x.Images).HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.Restrict);
    }
}

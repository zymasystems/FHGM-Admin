using FHGM.Admin.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Admin.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<BlogPost>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<BlogPost>().Property(x => x.Version).IsConcurrencyToken();
        builder.Entity<BlogPost>().HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AuditLog>().HasIndex(x => x.CreatedAt);
    }
}

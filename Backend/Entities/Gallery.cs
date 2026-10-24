using System.ComponentModel.DataAnnotations;

namespace FHGM.Backend.Entities;

public sealed class GalleryAlbum
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(180)] public string Slug { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    public bool IsPublished { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<GalleryImage> Images { get; set; } = [];
}

public sealed class GalleryImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlbumId { get; set; }
    public GalleryAlbum Album { get; set; } = null!;
    [MaxLength(255)] public string FileName { get; set; } = "";
    [MaxLength(300)] public string AltText { get; set; } = "";
    [MaxLength(1000)] public string Caption { get; set; } = "";
    [MaxLength(200)] public string StorageKey { get; set; } = "";
    [MaxLength(200)] public string StagingKey { get; set; } = "";
    [MaxLength(80)] public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string UploadedBy { get; set; } = "";
    public bool IsComplete { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDeleted { get; set; }
    public bool StagingCleaned { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UploadExpiresAt { get; set; }
}

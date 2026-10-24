using System.ComponentModel.DataAnnotations;

namespace FHGM.Backend.Entities;

public sealed class MinistryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(180)] public string Title { get; set; } = "";
    [MaxLength(200)] public string Slug { get; set; } = "";
    [MaxLength(5000)] public string Description { get; set; } = "";
    [MaxLength(300)] public string Location { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsPublished { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PopupAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(180)] public string Title { get; set; } = "";
    [MaxLength(3000)] public string Message { get; set; } = "";
    [MaxLength(80)] public string ButtonText { get; set; } = "";
    [MaxLength(1000)] public string ButtonUrl { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsPublished { get; set; }
    public int Priority { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

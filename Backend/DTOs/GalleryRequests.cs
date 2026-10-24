using System.ComponentModel.DataAnnotations;

namespace FHGM.Backend.DTOs;

public sealed record AlbumRequest(
    [Required, MaxLength(160)] string Title,
    [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), MaxLength(180)] string Slug,
    [MaxLength(1000)] string Description = "", bool IsPublished = false, int Version = 1);
public sealed record ImageUploadRequest(Guid AlbumId,
    [Required, MaxLength(255)] string FileName,
    [Required] string ContentType,
    [Range(16, 10485760)] long Size,
    [MaxLength(300)] string AltText = "");
public sealed record ImageUpdateRequest(
    [Required, MaxLength(300)] string AltText,
    [MaxLength(1000)] string Caption, bool IsPublished, int Version);

using System.ComponentModel.DataAnnotations;

namespace FHGM.Backend.DTOs;

public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public sealed record CreateUserRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password,
    [Required, MaxLength(120)] string DisplayName,
    [Required] string Role);
public sealed record ActiveRequest(bool IsActive);
public sealed record BlogRequest(
    [Required, MaxLength(180)] string Title,
    [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), MaxLength(200)] string Slug,
    [Required, MaxLength(500)] string Excerpt,
    [Required, MaxLength(50000)] string Content,
    [MaxLength(100)] string Category = "",
    [MaxLength(500)] string Tags = "",
    [MaxLength(180)] string SeoTitle = "",
    [MaxLength(320)] string SeoDescription = "",
    int Version = 1);
public sealed record TransitionRequest([Required] string Action, int Version, [MaxLength(2000)] string Note = "");

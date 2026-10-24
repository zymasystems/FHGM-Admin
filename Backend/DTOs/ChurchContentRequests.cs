using System.ComponentModel.DataAnnotations;

namespace FHGM.Backend.DTOs;

public sealed record EventRequest(
    [Required, MaxLength(180)] string Title,
    [Required, MaxLength(200), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] string Slug,
    [Required, MaxLength(5000)] string Description,
    [Required, MaxLength(300)] string Location,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsPublished, [Range(1, int.MaxValue)] int Version = 1) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartsAt == default || EndsAt == default || EndsAt <= StartsAt)
            yield return new ValidationResult("Choose an end date/time after the start date/time.", [nameof(EndsAt)]);
    }
}

public sealed record PopupRequest(
    [Required, MaxLength(180)] string Title,
    [Required, MaxLength(3000)] string Message,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsPublished,
    [MaxLength(80)] string ButtonText = "", [MaxLength(1000)] string ButtonUrl = "",
    [Range(0, 100)] int Priority = 0, [Range(1, int.MaxValue)] int Version = 1) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartsAt == default || EndsAt == default || EndsAt <= StartsAt)
            yield return new ValidationResult("Choose an end date/time after the start date/time.", [nameof(EndsAt)]);
        if (string.IsNullOrWhiteSpace(ButtonText) != string.IsNullOrWhiteSpace(ButtonUrl))
            yield return new ValidationResult("Provide both button text and a button link, or leave both empty.", [nameof(ButtonUrl)]);
        if (!string.IsNullOrEmpty(ButtonUrl) && !IsSafeLink(ButtonUrl))
            yield return new ValidationResult("Use an HTTPS link or a website path starting with a single slash.", [nameof(ButtonUrl)]);
    }
    private static bool IsSafeLink(string value) => !value.Any(char.IsControl) && !value.Contains('\\')
        && (value.StartsWith('/') && !value.StartsWith("//")
            || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo));
}

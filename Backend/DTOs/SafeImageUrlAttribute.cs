using System.ComponentModel.DataAnnotations;
namespace FHGM.Backend.DTOs;
public sealed class SafeImageUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is string text &&
        (text.Length == 0 || Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo));
}

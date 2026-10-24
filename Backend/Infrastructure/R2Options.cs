namespace FHGM.Backend.Infrastructure;

// Configuration contract for the next media stage. No credentials are sent to the frontend.
public sealed class R2Options
{
    public string AccountId { get; init; } = "";
    public string Bucket { get; init; } = "";
    public string AccessKeyId { get; init; } = "";
    public string SecretAccessKey { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "";
}

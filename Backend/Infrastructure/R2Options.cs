namespace FHGM.Backend.Infrastructure;

// Credentials remain on the backend. Endpoint override is for a local S3-compatible test server only.
public sealed class R2Options
{
    public string AccountId { get; init; } = "";
    public string Bucket { get; init; } = "";
    public string AccessKeyId { get; init; } = "";
    public string SecretAccessKey { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "";
    public string DevelopmentEndpoint { get; init; } = "";
}

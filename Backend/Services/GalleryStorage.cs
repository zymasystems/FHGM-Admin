using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FHGM.Backend.Entities;
using FHGM.Backend.Infrastructure;
using Microsoft.Extensions.Options;

namespace FHGM.Backend.Services;

public sealed class GalleryStorage(IOptions<R2Options> options, IHostEnvironment environment) : IDisposable
{
    private readonly R2Options settings = options.Value;
    private AmazonS3Client? client;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.AccountId) && !string.IsNullOrWhiteSpace(settings.Bucket)
        && !string.IsNullOrWhiteSpace(settings.AccessKeyId) && !string.IsNullOrWhiteSpace(settings.SecretAccessKey)
        && Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || environment.IsDevelopment() && uri.Scheme == "http");
    public string PublicUrl(string key) => settings.PublicBaseUrl.TrimEnd('/') + "/" + string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
    private AmazonS3Client Client
    {
        get
        {
            if (!IsConfigured) throw new InvalidOperationException("Gallery storage has not been configured.");
            var endpoint = environment.IsDevelopment() && !string.IsNullOrWhiteSpace(settings.DevelopmentEndpoint)
                ? settings.DevelopmentEndpoint : $"https://{settings.AccountId}.r2.cloudflarestorage.com";
            return client ??= new AmazonS3Client(new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey),
                new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "auto", SignatureVersion = "4", MaxErrorRetry = 1 });
        }
    }

    public Task<string> UploadUrlAsync(GalleryImage image) => Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
    {
        BucketName = settings.Bucket, Key = image.StagingKey, Verb = HttpVerb.PUT,
        Expires = image.UploadExpiresAt.UtcDateTime, ContentType = image.ContentType,
        Protocol = environment.IsDevelopment() && settings.DevelopmentEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTP : Protocol.HTTPS
    });

    public async Task VerifyAndPromoteAsync(GalleryImage image, CancellationToken cancellationToken)
    {
        var head = await Client.GetObjectMetadataAsync(settings.Bucket, image.StagingKey, cancellationToken);
        if (head.Headers.ContentLength != image.Size || !string.Equals(head.Headers.ContentType, image.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The uploaded file size or type does not match the selected image.");
        using var result = await Client.GetObjectAsync(new GetObjectRequest
        { BucketName = settings.Bucket, Key = image.StagingKey, ByteRange = new ByteRange(0, 15), EtagToMatch = head.ETag }, cancellationToken);
        var bytes = new byte[16];
        await result.ResponseStream.ReadExactlyAsync(bytes, cancellationToken);
        var valid = image.ContentType switch
        {
            "image/jpeg" => bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid) throw new InvalidDataException("The file does not have a valid JPEG, PNG or WebP signature.");
        // Clients can write only staging keys. Conditional copying prevents an upload URL from replacing a verified final image.
        await Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = settings.Bucket, SourceKey = image.StagingKey, DestinationBucket = settings.Bucket,
            DestinationKey = image.StorageKey, ETagToMatch = head.ETag, MetadataDirective = S3MetadataDirective.REPLACE,
            ContentType = image.ContentType
        }, cancellationToken);
    }

    public async Task DeleteAsync(GalleryImage image, CancellationToken cancellationToken)
    {
        await Client.DeleteObjectAsync(settings.Bucket, image.StorageKey, cancellationToken);
        await Client.DeleteObjectAsync(settings.Bucket, image.StagingKey, cancellationToken);
    }
    public Task DeleteStagingAsync(GalleryImage image, CancellationToken cancellationToken) =>
        Client.DeleteObjectAsync(settings.Bucket, image.StagingKey, cancellationToken);
    public void Dispose() => client?.Dispose();
}

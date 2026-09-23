using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ArkhivRobit.Services.Storage;

/// <summary>
/// Хмарне сховище через S3 API. Підходить для Cloudflare R2, Backblaze B2, Amazon S3, MinIO та інших.
/// Файли не проходять через сервер при скачуванні: користувач отримує тимчасове підписане посилання.
/// </summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly StorageOptions _options;
    private readonly bool _https;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ServiceUrl) || string.IsNullOrWhiteSpace(_options.Bucket))
            throw new InvalidOperationException("Для Storage__Provider=S3 задайте Storage__ServiceUrl і Storage__Bucket.");

        _https = _options.ServiceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            AuthenticationRegion = _options.Region,
            ForcePathStyle = true,
            // R2 і B2 не підтримують контрольні суми, які новіші версії SDK додають автоматично.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKey, _options.SecretKey), config);
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // Потоковий підпис вмісту не підтримується R2; через HTTPS вміст і так захищений.
            DisablePayloadSigning = _https,
        }, ct);
    }

    public Task<string?> GetDownloadUrlAsync(string key, string fileName, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.DownloadLinkMinutes),
            Protocol = _https ? Protocol.HTTPS : Protocol.HTTP,
        };
        // Щоб файл скачався з оригінальною (зокрема кириличною) назвою.
        request.ResponseHeaderOverrides.ContentDisposition =
            $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";

        return Task.FromResult<string?>(_client.GetPreSignedURL(request));
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(_options.Bucket, key, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await _client.DeleteObjectAsync(_options.Bucket, key, ct);

    public void Dispose() => _client.Dispose();
}

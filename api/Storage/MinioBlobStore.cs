using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using ScribAi.Api.Options;

namespace ScribAi.Api.Storage;

public class MinioBlobStore : IBlobStore
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly ILogger<MinioBlobStore> _log;

    public MinioBlobStore(IOptions<StorageOptions> opt, ILogger<MinioBlobStore> log)
    {
        var o = opt.Value;
        _bucket = o.Bucket;
        _log = log;
        _client = new MinioClient()
            .WithEndpoint(o.Endpoint)
            .WithCredentials(o.AccessKey, o.SecretKey)
            .WithSSL(o.UseSsl)
            .Build();
    }

    public async Task<string> PutAsync(Stream content, string filename, string contentType, CancellationToken ct)
    {
        var key = $"{DateTimeOffset.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}-{SanitizeFilename(filename)}";
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType), ct);
        return key;
    }

    public async Task<Stream> GetAsync(string key, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(key)
            .WithCallbackStream(s => s.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public Task DeleteAsync(string key, CancellationToken ct) =>
        _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(key), ct);

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        try
        {
            var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), ct);
            if (!exists)
            {
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
                _log.LogInformation("Created MinIO bucket {Bucket}", _bucket);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MinIO bucket check failed (non-fatal on startup)");
        }
    }

    private static string SanitizeFilename(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
        return safe.Length > 100 ? safe[..100] : safe;
    }
}

namespace ScribAi.Api.Storage;

public interface IBlobStore
{
    Task<string> PutAsync(Stream content, string filename, string contentType, CancellationToken ct);
    Task<Stream> GetAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task EnsureBucketAsync(CancellationToken ct);
}

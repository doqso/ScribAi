namespace ScribAi.Api.Options;

public class StorageOptions
{
    public const string Section = "Storage";
    public string Endpoint { get; set; } = "minio:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "scribai-originals";
    public bool UseSsl { get; set; } = false;
}

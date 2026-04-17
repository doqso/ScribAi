namespace ScribAi.Api.Data.Entities;

public class GlobalSettings
{
    public short Id { get; set; } = 1;
    public bool SeqEnabled { get; set; }
    public string? SeqUrl { get; set; }
    public byte[]? SeqApiKeyEncrypted { get; set; }
    public string SeqMinimumLevel { get; set; } = "Information";
    public string ApplicationName { get; set; } = "ScribAi";
    public string[] AllowedOrigins { get; set; } = [];
    public bool AllowAnyOrigin { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedBy { get; set; }
}

namespace ScribAi.Api.Data.Entities;

public enum ExtractionStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}

public class Extraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid ApiKeyId { get; set; }
    public Guid? SchemaId { get; set; }
    public string JsonSchemaSnapshot { get; set; } = string.Empty;

    public ExtractionStatus Status { get; set; } = ExtractionStatus.Queued;
    public string SourceFilename { get; set; } = string.Empty;
    public string Mime { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? StorageKey { get; set; }
    public string? Result { get; set; }
    public string? ExtractedText { get; set; }
    public string? Error { get; set; }
    public string Model { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public int? TokensIn { get; set; }
    public int? TokensOut { get; set; }
    public int AttemptCount { get; set; }

    public string? WebhookUrl { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

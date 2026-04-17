namespace ScribAi.Api.Data.Entities;

public class TenantSettings
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string? DefaultTextModel { get; set; }
    public string? VisionModel { get; set; }
    public int? OllamaTimeoutSeconds { get; set; }
    public int? WebhookMaxAttempts { get; set; }
    public int? WebhookTimeoutSeconds { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

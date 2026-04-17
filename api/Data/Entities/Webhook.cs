namespace ScribAi.Api.Data.Entities;

public class Webhook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid? ApiKeyId { get; set; }

    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string[] Events { get; set; } = ["extraction.succeeded", "extraction.failed"];
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WebhookId { get; set; }
    public Guid ExtractionId { get; set; }
    public string Event { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public int? StatusCode { get; set; }
    public string? Response { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

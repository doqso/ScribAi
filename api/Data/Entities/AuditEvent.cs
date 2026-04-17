namespace ScribAi.Api.Data.Entities;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

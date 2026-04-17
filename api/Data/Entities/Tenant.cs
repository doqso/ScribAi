namespace ScribAi.Api.Data.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<SchemaDefinition> Schemas { get; set; } = new List<SchemaDefinition>();
    public ICollection<Extraction> Extractions { get; set; } = new List<Extraction>();
    public ICollection<Webhook> Webhooks { get; set; } = new List<Webhook>();
}

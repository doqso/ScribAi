using ScribAi.Api.Data.Entities;

namespace ScribAi.Api.Auth;

public class TenantContext
{
    public Guid TenantId { get; init; }
    public Guid ApiKeyId { get; init; }
    public bool IsAdmin { get; init; }
    public bool StoreOriginals { get; init; }
    public string DefaultModel { get; init; } = string.Empty;

    public static TenantContext From(ApiKey key) => new()
    {
        TenantId = key.TenantId,
        ApiKeyId = key.Id,
        IsAdmin = key.IsAdmin,
        StoreOriginals = key.StoreOriginals,
        DefaultModel = key.DefaultModel,
    };
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Options;

namespace ScribAi.Api.Services;

public interface ITenantSettingsService
{
    Task<ResolvedTenantSettings> GetAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSettings> GetRawAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid tenantId, Action<TenantSettings> mutate, CancellationToken ct = default);
    void Invalidate(Guid tenantId);
}

public class TenantSettingsService(
    IDbContextFactory<ScribaiDbContext> dbFactory,
    IOptions<OllamaOptions> ollamaOpt,
    IOptions<ProcessingOptions> procOpt,
    IMemoryCache cache) : ITenantSettingsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static string Key(Guid id) => $"tenant_settings:{id:N}";

    public async Task<ResolvedTenantSettings> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (cache.TryGetValue<ResolvedTenantSettings>(Key(tenantId), out var cached) && cached is not null)
            return cached;

        var row = await GetRawAsync(tenantId, ct);
        var o = ollamaOpt.Value;
        var p = procOpt.Value;

        var resolved = new ResolvedTenantSettings(
            TenantId: tenantId,
            DefaultTextModel: row.DefaultTextModel ?? o.DefaultModel,
            VisionModel: row.VisionModel ?? o.VisionModel,
            OllamaTimeoutSeconds: row.OllamaTimeoutSeconds ?? o.TimeoutSeconds,
            WebhookMaxAttempts: row.WebhookMaxAttempts ?? p.WebhookMaxAttempts,
            WebhookTimeoutSeconds: row.WebhookTimeoutSeconds ?? 15,
            Think: row.Think
        );

        cache.Set(Key(tenantId), resolved, CacheTtl);
        return resolved;
    }

    public async Task<TenantSettings> GetRawAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (row is null)
        {
            row = new TenantSettings { TenantId = tenantId };
            db.TenantSettings.Add(row);
            await db.SaveChangesAsync(ct);
        }
        return row;
    }

    public async Task UpdateAsync(Guid tenantId, Action<TenantSettings> mutate, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (row is null)
        {
            row = new TenantSettings { TenantId = tenantId };
            db.TenantSettings.Add(row);
        }
        mutate(row);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        Invalidate(tenantId);
    }

    public void Invalidate(Guid tenantId) => cache.Remove(Key(tenantId));
}

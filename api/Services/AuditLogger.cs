using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using System.Text.Json;

namespace ScribAi.Api.Services;

public interface IAuditLogger
{
    Task LogAsync(string eventType, string? target = null, object? details = null, Guid? tenantId = null, Guid? apiKeyId = null, CancellationToken ct = default);
    Task LogAsync(HttpContext ctx, string eventType, string? target = null, object? details = null, CancellationToken ct = default);
}

public class AuditLogger(
    IDbContextFactory<ScribaiDbContext> dbFactory,
    ILogger<AuditLogger> log) : IAuditLogger
{
    public async Task LogAsync(string eventType, string? target = null, object? details = null,
        Guid? tenantId = null, Guid? apiKeyId = null, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = eventType,
                Target = target,
                Details = details is null ? null : JsonSerializer.Serialize(details),
                TenantId = tenantId,
                ApiKeyId = apiKeyId
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Audit log write failed for {Event}", eventType);
        }
    }

    public Task LogAsync(HttpContext ctx, string eventType, string? target = null, object? details = null, CancellationToken ct = default)
    {
        var t = ctx.Items["TenantContext"] as TenantContext;
        return LogAsync(eventType, target, details, t?.TenantId, t?.ApiKeyId, ct);
    }
}

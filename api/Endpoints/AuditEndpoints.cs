using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;

namespace ScribAi.Api.Endpoints;

public static class AuditEndpoints
{
    public record AuditEventDto(Guid Id, Guid? TenantId, Guid? ApiKeyId, string EventType, string? Target, string? Details, DateTimeOffset CreatedAt);
    public record AuditPage(int Total, int Page, int PageSize, List<AuditEventDto> Items);

    public static void MapAudit(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/admin/audit", async (HttpContext ctx, ScribaiDbContext db, string? eventType, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);

            var q = db.AuditEvents.AsNoTracking()
                .Where(a => a.TenantId == t.TenantId || a.TenantId == null);
            if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(a => a.EventType == eventType);
            if (from is not null) q = q.Where(a => a.CreatedAt >= from);
            if (to is not null) q = q.Where(a => a.CreatedAt <= to);

            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new AuditEventDto(a.Id, a.TenantId, a.ApiKeyId, a.EventType, a.Target, a.Details, a.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(new AuditPage(total, page, pageSize, items));
        }).WithTags("Audit");
    }
}

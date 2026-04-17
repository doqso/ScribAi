using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Jobs;
using ScribAi.Api.Services;
using System.Security.Cryptography;

namespace ScribAi.Api.Endpoints;

public static class WebhooksEndpoints
{
    public record WebhookCreateRequest(string Url, string[]? Events);
    public record WebhookDto(Guid Id, string Url, string[] Events, bool Active, string? Secret, Guid? ApiKeyId, DateTimeOffset CreatedAt);

    public static void MapWebhooks(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/webhooks").WithTags("Webhooks");

        g.MapGet("/", async (HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var q = db.Webhooks.AsNoTracking().Where(w => w.TenantId == t.TenantId);
            if (!t.IsAdmin) q = q.Where(w => w.ApiKeyId == t.ApiKeyId);
            var list = await q
                .Select(w => new WebhookDto(w.Id, w.Url, w.Events, w.Active, null, w.ApiKeyId, w.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        g.MapPost("/", async (WebhookCreateRequest req, HttpContext ctx, ScribaiDbContext db, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!Uri.TryCreate(req.Url, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "invalid_url" });

            var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
            var hook = new Webhook
            {
                TenantId = t.TenantId,
                ApiKeyId = t.ApiKeyId,
                Url = req.Url,
                Secret = secret,
                Events = req.Events is { Length: > 0 } ? req.Events : ["extraction.succeeded", "extraction.failed"],
                Active = true
            };
            db.Webhooks.Add(hook);
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(ctx, "webhook.created", target: hook.Id.ToString(),
                details: new { hook.Url, hook.Events }, ct: ct);
            return Results.Created($"/v1/webhooks/{hook.Id}",
                new WebhookDto(hook.Id, hook.Url, hook.Events, hook.Active, hook.Secret, hook.ApiKeyId, hook.CreatedAt));
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var q = db.Webhooks.Where(w => w.Id == id && w.TenantId == t.TenantId);
            if (!t.IsAdmin) q = q.Where(w => w.ApiKeyId == t.ApiKeyId);
            var deleted = await q.ExecuteDeleteAsync(ct);
            if (deleted > 0) await audit.LogAsync(ctx, "webhook.deleted", target: id.ToString(), ct: ct);
            return deleted > 0 ? Results.NoContent() : Results.NotFound();
        });

        g.MapPost("/{id:guid}/test", async (Guid id, HttpContext ctx, ScribaiDbContext db, WebhookDispatcher dispatcher, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var hook = await db.Webhooks.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == t.TenantId, ct);
            if (hook is null) return Results.NotFound();
            if (!t.IsAdmin && hook.ApiKeyId != t.ApiKeyId) return Results.NotFound();

            var fake = new Extraction
            {
                Id = Guid.NewGuid(),
                TenantId = t.TenantId,
                ApiKeyId = t.ApiKeyId,
                Status = ExtractionStatus.Succeeded,
                SourceFilename = "test.pdf",
                Mime = "application/pdf",
                Result = "{\"test\":true}",
                FinishedAt = DateTimeOffset.UtcNow,
                WebhookUrl = hook.Url
            };
            await dispatcher.DispatchAsync(fake, ct);
            return Results.Ok(new { sent = true });
        });
    }
}

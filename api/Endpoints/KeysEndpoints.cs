using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Services;

namespace ScribAi.Api.Endpoints;

public static class KeysEndpoints
{
    public record KeyCreateRequest(string Label, bool StoreOriginals, string? DefaultModel, bool IsAdmin);
    public record KeyDto(Guid Id, string Label, string Prefix, bool IsAdmin, bool StoreOriginals, string DefaultModel, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);
    public record KeyCreatedDto(Guid Id, string Label, string Key, string Prefix, bool IsAdmin, bool StoreOriginals, string DefaultModel);

    public static void MapKeys(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/keys").WithTags("Keys");

        g.MapGet("/", async (HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();

            var list = await db.ApiKeys.AsNoTracking()
                .Where(k => k.TenantId == t.TenantId)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new KeyDto(k.Id, k.Label, k.KeyPrefix, k.IsAdmin, k.StoreOriginals, k.DefaultModel, k.CreatedAt, k.LastUsedAt, k.RevokedAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        g.MapPost("/", async (KeyCreateRequest req, HttpContext ctx, ScribaiDbContext db, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();

            var plain = ApiKeyHasher.Generate();
            var key = new ApiKey
            {
                TenantId = t.TenantId,
                Label = req.Label,
                KeyHash = ApiKeyHasher.Hash(plain),
                KeyPrefix = ApiKeyHasher.Prefix(plain),
                StoreOriginals = req.StoreOriginals,
                DefaultModel = string.IsNullOrWhiteSpace(req.DefaultModel) ? "qwen2.5:7b-instruct" : req.DefaultModel!,
                IsAdmin = req.IsAdmin
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(ctx, "api_key.created", target: key.Id.ToString(),
                details: new { key.Label, key.IsAdmin, key.StoreOriginals, key.DefaultModel }, ct: ct);
            return Results.Created($"/v1/keys/{key.Id}",
                new KeyCreatedDto(key.Id, key.Label, plain, key.KeyPrefix, key.IsAdmin, key.StoreOriginals, key.DefaultModel));
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();
            if (id == t.ApiKeyId) return Results.BadRequest(new { error = "cannot_revoke_self" });

            var updated = await db.ApiKeys
                .Where(k => k.Id == id && k.TenantId == t.TenantId && k.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.RevokedAt, DateTimeOffset.UtcNow), ct);
            if (updated > 0) await audit.LogAsync(ctx, "api_key.revoked", target: id.ToString(), ct: ct);
            return updated > 0 ? Results.NoContent() : Results.NotFound();
        });
    }
}

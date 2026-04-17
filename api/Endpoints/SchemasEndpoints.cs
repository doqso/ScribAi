using Microsoft.EntityFrameworkCore;
using NJsonSchema;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Services;

namespace ScribAi.Api.Endpoints;

public static class SchemasEndpoints
{
    public record SchemaCreateRequest(string Name, string JsonSchema, string? Description);
    public record SchemaUpdateRequest(string JsonSchema, string? Description);
    public record SchemaDto(
        Guid Id,
        string Name,
        int Version,
        string JsonSchema,
        string? Description,
        Guid? ApiKeyId,
        string Scope,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public static void MapSchemas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/schemas").WithTags("Schemas");

        g.MapGet("/", async (HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var q = db.Schemas.AsNoTracking().Where(s => s.TenantId == t.TenantId);
            if (!t.IsAdmin) q = q.Where(s => s.ApiKeyId == null || s.ApiKeyId == t.ApiKeyId);

            var items = await q.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
            return Results.Ok(items.Select(ToDto));
        });

        g.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var s = await db.Schemas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            if (s is null) return Results.NotFound();
            if (!t.IsAdmin && s.ApiKeyId != null && s.ApiKeyId != t.ApiKeyId) return Results.NotFound();
            return Results.Ok(ToDto(s));
        });

        // Prefer the caller's own version over the global when name collides.
        g.MapGet("/by-name/{name}/latest", async (string name, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var own = await db.Schemas.AsNoTracking()
                .Where(x => x.TenantId == t.TenantId && x.Name == name && x.ApiKeyId == t.ApiKeyId)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(ct);

            var s = own ?? await db.Schemas.AsNoTracking()
                .Where(x => x.TenantId == t.TenantId && x.Name == name && x.ApiKeyId == null)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(ct);

            return s is null ? Results.NotFound() : Results.Ok(ToDto(s));
        });

        g.MapPost("/", async (SchemaCreateRequest req, HttpContext ctx, ScribaiDbContext db, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { error = "name_required" });

            try { await JsonSchema.FromJsonAsync(req.JsonSchema, ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json_schema", detail = ex.Message }); }

            var scopedKey = t.IsAdmin ? (Guid?)null : t.ApiKeyId;
            var latest = await db.Schemas
                .Where(x => x.TenantId == t.TenantId && x.ApiKeyId == scopedKey && x.Name == req.Name)
                .MaxAsync(x => (int?)x.Version, ct) ?? 0;

            var entity = new SchemaDefinition
            {
                TenantId = t.TenantId,
                ApiKeyId = scopedKey,
                Name = req.Name,
                Version = latest + 1,
                JsonSchema = req.JsonSchema,
                Description = req.Description
            };
            db.Schemas.Add(entity);
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(ctx, "schema.created", target: entity.Id.ToString(),
                details: new { entity.Name, entity.Version, scope = entity.ApiKeyId is null ? "global" : "personal" }, ct: ct);
            return Results.Created($"/v1/schemas/{entity.Id}", ToDto(entity));
        });

        g.MapPut("/{id:guid}", async (Guid id, SchemaUpdateRequest req, HttpContext ctx, ScribaiDbContext db, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var s = await db.Schemas.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            if (s is null) return Results.NotFound();
            if (!CanMutate(s, t)) return Results.NotFound();

            try { await JsonSchema.FromJsonAsync(req.JsonSchema, ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json_schema", detail = ex.Message }); }

            s.JsonSchema = req.JsonSchema;
            s.Description = req.Description;
            s.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(ctx, "schema.updated", target: s.Id.ToString(),
                details: new { s.Name, s.Version, scope = s.ApiKeyId is null ? "global" : "personal" }, ct: ct);
            return Results.Ok(ToDto(s));
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var s = await db.Schemas.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            if (s is null) return Results.NotFound();
            if (!CanMutate(s, t)) return Results.NotFound();

            db.Schemas.Remove(s);
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(ctx, "schema.deleted", target: id.ToString(), ct: ct);
            return Results.NoContent();
        });
    }

    private static bool CanMutate(SchemaDefinition s, TenantContext t)
    {
        if (s.ApiKeyId is null) return t.IsAdmin;          // global — admin only
        return s.ApiKeyId == t.ApiKeyId || t.IsAdmin;       // personal — owner or admin
    }

    private static SchemaDto ToDto(SchemaDefinition s) => new(
        s.Id, s.Name, s.Version, s.JsonSchema, s.Description,
        s.ApiKeyId,
        s.ApiKeyId is null ? "global" : "personal",
        s.CreatedAt, s.UpdatedAt);
}

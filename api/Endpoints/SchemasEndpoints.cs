using Microsoft.EntityFrameworkCore;
using NJsonSchema;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;

namespace ScribAi.Api.Endpoints;

public static class SchemasEndpoints
{
    public record SchemaCreateRequest(string Name, string JsonSchema, string? Description);
    public record SchemaDto(Guid Id, string Name, int Version, string JsonSchema, string? Description, DateTimeOffset CreatedAt);

    public static void MapSchemas(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/schemas").WithTags("Schemas");

        g.MapGet("/", async (HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var items = await db.Schemas
                .AsNoTracking()
                .Where(s => s.TenantId == t.TenantId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SchemaDto(s.Id, s.Name, s.Version, s.JsonSchema, s.Description, s.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(items);
        });

        g.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var s = await db.Schemas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            return s is null
                ? Results.NotFound()
                : Results.Ok(new SchemaDto(s.Id, s.Name, s.Version, s.JsonSchema, s.Description, s.CreatedAt));
        });

        g.MapGet("/by-name/{name}/latest", async (string name, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var s = await db.Schemas.AsNoTracking()
                .Where(x => x.TenantId == t.TenantId && x.Name == name)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(ct);
            return s is null
                ? Results.NotFound()
                : Results.Ok(new SchemaDto(s.Id, s.Name, s.Version, s.JsonSchema, s.Description, s.CreatedAt));
        });

        g.MapPost("/", async (SchemaCreateRequest req, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { error = "name_required" });

            try { await JsonSchema.FromJsonAsync(req.JsonSchema, ct); }
            catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json_schema", detail = ex.Message }); }

            var latest = await db.Schemas
                .Where(x => x.TenantId == t.TenantId && x.Name == req.Name)
                .MaxAsync(x => (int?)x.Version, ct) ?? 0;

            var entity = new SchemaDefinition
            {
                TenantId = t.TenantId,
                Name = req.Name,
                Version = latest + 1,
                JsonSchema = req.JsonSchema,
                Description = req.Description
            };
            db.Schemas.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/v1/schemas/{entity.Id}", new SchemaDto(entity.Id, entity.Name, entity.Version, entity.JsonSchema, entity.Description, entity.CreatedAt));
        });

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var deleted = await db.Schemas
                .Where(s => s.Id == id && s.TenantId == t.TenantId)
                .ExecuteDeleteAsync(ct);
            return deleted > 0 ? Results.NoContent() : Results.NotFound();
        });
    }
}

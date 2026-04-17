using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NJsonSchema;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Jobs;
using ScribAi.Api.Options;
using ScribAi.Api.Services;
using ScribAi.Api.Storage;

namespace ScribAi.Api.Endpoints;

public static class ExtractionsEndpoints
{
    public record ExtractionDto(Guid Id, string Status, string SourceFilename, string Mime, long SizeBytes,
        string? Result, string? Error, string Model, string ExtractionMethod,
        int? TokensIn, int? TokensOut, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);

    public static void MapExtractions(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/extractions").WithTags("Extractions");

        g.MapPost("/", HandleCreate).DisableAntiforgery();

        g.MapGet("/", async (HttpContext ctx, ScribaiDbContext db, string? status, int page, int pageSize, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var query = db.Extractions.AsNoTracking().Where(e => e.TenantId == t.TenantId);
            if (!t.IsAdmin) query = query.Where(e => e.ApiKeyId == t.ApiKeyId);
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ExtractionStatus>(status, true, out var st))
                query = query.Where(e => e.Status == st);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(e => ToDto(e))
                .ToListAsync(ct);

            return Results.Ok(new { total, page, pageSize, items });
        });

        g.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var e = await db.Extractions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            if (e is null) return Results.NotFound();
            if (!t.IsAdmin && e.ApiKeyId != t.ApiKeyId) return Results.NotFound();
            return Results.Ok(ToDto(e));
        });

        g.MapPost("/{id:guid}/rerun", HandleRerun);

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, ScribaiDbContext db, IBlobStore blobs, IAuditLogger audit, ILogger<Program> log, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var e = await db.Extractions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            if (e is null) return Results.NotFound();
            if (!t.IsAdmin && e.ApiKeyId != t.ApiKeyId) return Results.NotFound();

            var storageKey = e.StorageKey;
            db.Extractions.Remove(e);
            await db.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(storageKey))
            {
                try { await blobs.DeleteAsync(storageKey, ct); }
                catch (Exception ex) { log.LogWarning(ex, "Failed to delete blob {Key} for extraction {Id}", storageKey, id); }
            }

            await audit.LogAsync(ctx, "extraction.deleted", target: id.ToString(),
                details: new { e.SourceFilename, storageKey }, ct: ct);
            return Results.NoContent();
        });
    }

    public record RerunRequest(Guid? SchemaId, string? Schema, string? Model, bool Async = false);

    private static async Task<IResult> HandleRerun(
        Guid id,
        RerunRequest req,
        HttpContext ctx,
        ScribaiDbContext db,
        IBlobStore blobs,
        IJobQueue queue,
        ExtractionService service,
        IOptions<ProcessingOptions> procOpt,
        CancellationToken ct)
    {
        var t = ctx.Tenant();
        var original = await db.Extractions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
        if (original is null) return Results.NotFound();
        if (!t.IsAdmin && original.ApiKeyId != t.ApiKeyId) return Results.NotFound();
        if (string.IsNullOrEmpty(original.StorageKey))
            return Results.BadRequest(new { error = "original_not_stored", detail = "Re-run requires the original file to have been stored (store_originals=true on the API key used for upload)." });

        var schemaJson = req.Schema;
        Guid? schemaId = null;
        if (req.SchemaId is Guid sid)
        {
            var schema = await db.Schemas.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && s.TenantId == t.TenantId
                && (s.ApiKeyId == null || s.ApiKeyId == t.ApiKeyId), ct);
            if (schema is null) return Results.BadRequest(new { error = "schema_not_found" });
            schemaJson = schema.JsonSchema;
            schemaId = schema.Id;
        }
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            schemaJson = original.JsonSchemaSnapshot;
            schemaId = original.SchemaId;
        }

        try { await JsonSchema.FromJsonAsync(schemaJson, ct); }
        catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json_schema", detail = ex.Message }); }

        // Only admins may override the model; non-admins always inherit tenant default (resolved in ExtractionService)
        var model = t.IsAdmin && !string.IsNullOrWhiteSpace(req.Model) ? req.Model : string.Empty;

        var rerun = new Extraction
        {
            TenantId = t.TenantId,
            ApiKeyId = t.ApiKeyId,
            SchemaId = schemaId,
            JsonSchemaSnapshot = schemaJson,
            SourceFilename = original.SourceFilename,
            Mime = original.Mime,
            SizeBytes = original.SizeBytes,
            StorageKey = original.StorageKey,
            Status = ExtractionStatus.Queued,
            Model = model
        };
        db.Extractions.Add(rerun);
        await db.SaveChangesAsync(ct);

        var proc = procOpt.Value;
        var shouldAsync = req.Async || rerun.SizeBytes > proc.SyncMaxBytes;

        if (shouldAsync)
        {
            await queue.EnqueueAsync(new ExtractionJob(rerun.Id, t.TenantId), ct);
            return Results.Accepted($"/v1/extractions/{rerun.Id}", ToDto(rerun));
        }

        await using var content = await blobs.GetAsync(original.StorageKey, ct);
        var processed = await service.ProcessAsync(rerun, content, storeOriginal: false, model, ct);
        return processed.Status == ExtractionStatus.Succeeded
            ? Results.Ok(ToDto(processed))
            : Results.Json(ToDto(processed), statusCode: 422);
    }

    private static async Task<IResult> HandleCreate(
        HttpContext ctx,
        ScribaiDbContext db,
        IBlobStore blobs,
        IJobQueue queue,
        ExtractionService service,
        IOptions<ProcessingOptions> procOpt,
        CancellationToken ct)
    {
        if (!ctx.Request.HasFormContentType) return Results.BadRequest(new { error = "expected_multipart" });
        var form = await ctx.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0) return Results.BadRequest(new { error = "file_required" });

        var t = ctx.Tenant();
        var proc = procOpt.Value;
        if (file.Length > proc.MaxUploadBytes)
            return Results.BadRequest(new { error = "file_too_large", maxBytes = proc.MaxUploadBytes });

        var schemaJson = form["schema"].ToString();
        var schemaIdStr = form["schemaId"].ToString();
        Guid? schemaId = null;

        if (!string.IsNullOrEmpty(schemaIdStr))
        {
            if (!Guid.TryParse(schemaIdStr, out var sid)) return Results.BadRequest(new { error = "invalid_schema_id" });
            var schema = await db.Schemas.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && s.TenantId == t.TenantId
                && (s.ApiKeyId == null || s.ApiKeyId == t.ApiKeyId), ct);
            if (schema is null) return Results.BadRequest(new { error = "schema_not_found" });
            schemaJson = schema.JsonSchema;
            schemaId = schema.Id;
        }

        if (string.IsNullOrWhiteSpace(schemaJson))
            return Results.BadRequest(new { error = "schema_required" });

        try { await JsonSchema.FromJsonAsync(schemaJson, ct); }
        catch (Exception ex) { return Results.BadRequest(new { error = "invalid_json_schema", detail = ex.Message }); }

        var forceAsync = form["async"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
        var webhookUrl = form["webhookUrl"].ToString();
        // Only admins may override the model via the `model` form field; non-admins inherit tenant default
        var model = t.IsAdmin ? form["model"].ToString() : string.Empty;

        var shouldAsync = forceAsync || file.Length > proc.SyncMaxBytes;

        var extraction = new Extraction
        {
            TenantId = t.TenantId,
            ApiKeyId = t.ApiKeyId,
            SchemaId = schemaId,
            JsonSchemaSnapshot = schemaJson,
            SourceFilename = file.FileName,
            Mime = file.ContentType ?? "application/octet-stream",
            SizeBytes = file.Length,
            Status = ExtractionStatus.Queued,
            Model = model,
            WebhookUrl = string.IsNullOrWhiteSpace(webhookUrl) ? null : webhookUrl
        };

        db.Extractions.Add(extraction);
        await db.SaveChangesAsync(ct);

        await using var upload = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        if (shouldAsync)
        {
            extraction.StorageKey = await blobs.PutAsync(buffer, file.FileName, extraction.Mime, ct);
            await db.SaveChangesAsync(ct);
            await queue.EnqueueAsync(new ExtractionJob(extraction.Id, t.TenantId), ct);
            return Results.Accepted($"/v1/extractions/{extraction.Id}", ToDto(extraction));
        }

        buffer.Position = 0;
        var processed = await service.ProcessAsync(extraction, buffer, t.StoreOriginals, model, ct);
        return processed.Status == ExtractionStatus.Succeeded
            ? Results.Ok(ToDto(processed))
            : Results.Json(ToDto(processed), statusCode: 422);
    }

    private static ExtractionDto ToDto(Extraction e) => new(
        e.Id, e.Status.ToString().ToLowerInvariant(), e.SourceFilename, e.Mime, e.SizeBytes,
        e.Result, e.Error, e.Model, e.ExtractionMethod, e.TokensIn, e.TokensOut,
        e.CreatedAt, e.StartedAt, e.FinishedAt);
}

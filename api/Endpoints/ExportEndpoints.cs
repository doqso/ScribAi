using CsvHelper;
using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ScribAi.Api.Endpoints;

public static class ExportEndpoints
{
    public static void MapExports(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/extractions/{id:guid}/original", async (Guid id, HttpContext ctx, ScribaiDbContext db, ScribAi.Api.Storage.IBlobStore blobs, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var e = await db.Extractions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            if (e is null) return Results.NotFound();
            if (string.IsNullOrEmpty(e.StorageKey)) return Results.NotFound(new { error = "not_stored" });

            var stream = await blobs.GetAsync(e.StorageKey, ct);
            return Results.Stream(stream, e.Mime, e.SourceFilename, enableRangeProcessing: true);
        }).WithTags("Export");

        app.MapGet("/v1/extractions/{id:guid}/export.json", async (Guid id, HttpContext ctx, ScribaiDbContext db, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var e = await db.Extractions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == t.TenantId, ct);
            if (e is null) return Results.NotFound();
            if (string.IsNullOrEmpty(e.Result)) return Results.BadRequest(new { error = "no_result" });

            var bytes = Encoding.UTF8.GetBytes(Pretty(e.Result));
            var filename = SafeFilename(Path.GetFileNameWithoutExtension(e.SourceFilename)) + ".json";
            return Results.File(bytes, "application/json", filename);
        }).WithTags("Export");

        app.MapGet("/v1/extractions/export.csv", async (
            HttpContext ctx,
            ScribaiDbContext db,
            Guid? schemaId,
            string? schemaName,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            var q = db.Extractions.AsNoTracking()
                .Where(x => x.TenantId == t.TenantId
                            && x.Status == ExtractionStatus.Succeeded
                            && x.Result != null);

            if (schemaId is Guid sid) q = q.Where(x => x.SchemaId == sid);
            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                var sids = db.Schemas.Where(s => s.TenantId == t.TenantId && s.Name == schemaName).Select(s => (Guid?)s.Id);
                q = q.Where(x => sids.Contains(x.SchemaId));
            }
            if (from is not null) q = q.Where(x => x.CreatedAt >= from);
            if (to is not null) q = q.Where(x => x.CreatedAt <= to);

            var items = await q.OrderBy(x => x.CreatedAt).Take(10_000)
                .Select(x => new { x.Id, x.SourceFilename, x.CreatedAt, x.Result })
                .ToListAsync(ct);

            if (items.Count == 0)
                return Results.BadRequest(new { error = "no_results" });

            var rows = items.Select(it =>
            {
                var dict = new Dictionary<string, string>
                {
                    ["_extraction_id"] = it.Id.ToString(),
                    ["_source_filename"] = it.SourceFilename,
                    ["_created_at"] = it.CreatedAt.ToString("O")
                };
                if (!string.IsNullOrEmpty(it.Result))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(it.Result);
                        Flatten(doc.RootElement, "", dict);
                    }
                    catch { /* ignore */ }
                }
                return dict;
            }).ToList();

            var columns = rows.SelectMany(r => r.Keys).Distinct().OrderBy(k => k switch
            {
                "_extraction_id" => 0,
                "_source_filename" => 1,
                "_created_at" => 2,
                _ => 100
            }).ThenBy(k => k).ToList();

            using var ms = new MemoryStream();
            using (var writer = new StreamWriter(ms, new UTF8Encoding(true), leaveOpen: true))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                foreach (var c in columns) csv.WriteField(c);
                await csv.NextRecordAsync();
                foreach (var row in rows)
                {
                    foreach (var c in columns) csv.WriteField(row.TryGetValue(c, out var v) ? v : string.Empty);
                    await csv.NextRecordAsync();
                }
                await writer.FlushAsync(ct);
            }

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return Results.File(ms.ToArray(), "text/csv", $"scribai-export-{stamp}.csv");
        }).WithTags("Export");
    }

    private static string Pretty(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return json; }
    }

    private static void Flatten(JsonElement el, string prefix, Dictionary<string, string> into)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    var k = string.IsNullOrEmpty(prefix) ? p.Name : $"{prefix}.{p.Name}";
                    Flatten(p.Value, k, into);
                }
                break;
            case JsonValueKind.Array:
                into[prefix] = el.GetRawText();
                break;
            case JsonValueKind.String: into[prefix] = el.GetString() ?? ""; break;
            case JsonValueKind.Number: into[prefix] = el.GetRawText(); break;
            case JsonValueKind.True: into[prefix] = "true"; break;
            case JsonValueKind.False: into[prefix] = "false"; break;
            case JsonValueKind.Null: into[prefix] = ""; break;
        }
    }

    private static string SafeFilename(string s)
    {
        var safe = new string(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray());
        return string.IsNullOrEmpty(safe) ? "extraction" : safe;
    }
}

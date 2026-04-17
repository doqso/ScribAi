using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NJsonSchema;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Options;
using ScribAi.Api.Services;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ScribAi.Api.Endpoints;

public static class StreamingEndpoint
{
    public static void MapStreaming(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/extractions/stream", HandleStream).DisableAntiforgery().WithTags("Extractions");
    }

    private static async Task HandleStream(
        HttpContext ctx,
        ScribaiDbContext db,
        ExtractionService service,
        IOptions<ProcessingOptions> procOpt,
        CancellationToken ct)
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";

        async Task WriteEvent(string name, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var chunk = $"event: {name}\ndata: {json}\n\n";
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(chunk), ct);
            await ctx.Response.Body.FlushAsync(ct);
        }

        if (!ctx.Request.HasFormContentType)
        {
            await WriteEvent("error", new { error = "expected_multipart" });
            return;
        }

        var form = await ctx.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            await WriteEvent("error", new { error = "file_required" });
            return;
        }

        var t = ctx.Tenant();
        var proc = procOpt.Value;
        if (file.Length > proc.MaxUploadBytes)
        {
            await WriteEvent("error", new { error = "file_too_large", maxBytes = proc.MaxUploadBytes });
            return;
        }

        var schemaJson = form["schema"].ToString();
        var schemaIdStr = form["schemaId"].ToString();
        Guid? schemaId = null;
        if (!string.IsNullOrEmpty(schemaIdStr))
        {
            if (!Guid.TryParse(schemaIdStr, out var sid))
            {
                await WriteEvent("error", new { error = "invalid_schema_id" });
                return;
            }
            var schema = await db.Schemas.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && s.TenantId == t.TenantId, ct);
            if (schema is null)
            {
                await WriteEvent("error", new { error = "schema_not_found" });
                return;
            }
            schemaJson = schema.JsonSchema;
            schemaId = schema.Id;
        }
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            await WriteEvent("error", new { error = "schema_required" });
            return;
        }
        try { await JsonSchema.FromJsonAsync(schemaJson, ct); }
        catch (Exception ex) { await WriteEvent("error", new { error = "invalid_json_schema", detail = ex.Message }); return; }

        var model = form["model"].ToString();
        if (string.IsNullOrWhiteSpace(model)) model = t.DefaultModel;
        var webhookUrl = form["webhookUrl"].ToString();

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
        await WriteEvent("created", new { id = extraction.Id });

        var channel = Channel.CreateUnbounded<ExtractionService.ProgressEvent>(new UnboundedChannelOptions { SingleReader = true });
        var progress = new Progress<ExtractionService.ProgressEvent>(e => channel.Writer.TryWrite(e));

        await using var upload = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var processTask = Task.Run(async () =>
        {
            try
            {
                var result = await service.ProcessAsync(extraction, buffer, t.StoreOriginals, model, progress, ct);
                return result;
            }
            finally { channel.Writer.Complete(); }
        }, ct);

        await foreach (var evt in channel.Reader.ReadAllAsync(ct))
        {
            await WriteEvent("progress", evt);
        }

        var processed = await processTask;
        await WriteEvent(processed.Status == ExtractionStatus.Succeeded ? "succeeded" : "failed", new
        {
            id = processed.Id,
            status = processed.Status.ToString().ToLowerInvariant(),
            result = processed.Result,
            error = processed.Error,
            model = processed.Model,
            extractionMethod = processed.ExtractionMethod,
            tokensIn = processed.TokensIn,
            tokensOut = processed.TokensOut
        });
    }
}

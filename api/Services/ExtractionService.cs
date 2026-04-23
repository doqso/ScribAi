using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Jobs;
using ScribAi.Api.Pipeline;
using ScribAi.Api.Pipeline.Llm;
using ScribAi.Api.Storage;
using Serilog.Context;
using System.Diagnostics;

namespace ScribAi.Api.Services;

public class ExtractionService(
    ScribaiDbContext db,
    IDocumentRouter router,
    IOllamaExtractor llm,
    IBlobStore blobs,
    WebhookDispatcher webhooks,
    ITenantSettingsService tenantSettings,
    ILogger<ExtractionService> log)
{
    public record ProgressEvent(string Step, string? Detail = null, long? ElapsedMs = null);

    public Task<Extraction> ProcessAsync(Extraction extraction, Stream content, bool storeOriginal, string model, CancellationToken ct)
        => ProcessAsync(extraction, content, storeOriginal, model, null, ct);

    public async Task<Extraction> ProcessAsync(Extraction extraction, Stream content, bool storeOriginal, string model, IProgress<ProgressEvent>? progress, CancellationToken ct)
    {
        using var _ext = LogContext.PushProperty("ExtractionId", extraction.Id);
        using var _ten = LogContext.PushProperty("TenantId", extraction.TenantId);
        using var _file = LogContext.PushProperty("SourceFilename", extraction.SourceFilename);
        using var _mime = LogContext.PushProperty("Mime", extraction.Mime);

        var totalSw = Stopwatch.StartNew();
        var settings = await tenantSettings.GetAsync(extraction.TenantId, ct);
        var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model : settings.DefaultTextModel;
        var llmTimeout = TimeSpan.FromSeconds(settings.OllamaTimeoutSeconds);

        extraction.Status = ExtractionStatus.Running;
        extraction.StartedAt = DateTimeOffset.UtcNow;
        extraction.Model = effectiveModel;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Extraction started. size={Size} model={Model} timeout={Timeout}s",
            extraction.SizeBytes, effectiveModel, settings.OllamaTimeoutSeconds);
        progress?.Report(new ProgressEvent("started", $"size={extraction.SizeBytes}"));

        try
        {
            if (storeOriginal)
            {
                progress?.Report(new ProgressEvent("storing_original"));
                var swStore = Stopwatch.StartNew();
                content.Position = 0;
                extraction.StorageKey = await blobs.PutAsync(content, extraction.SourceFilename, extraction.Mime, ct);
                content.Position = 0;
                log.LogInformation("Original stored. storage_key={Key} duration_ms={Ms}", extraction.StorageKey, swStore.ElapsedMilliseconds);
            }

            progress?.Report(new ProgressEvent("extracting_text"));
            var swRouter = Stopwatch.StartNew();
            var doc = await router.ExtractAsync(content, extraction.SourceFilename, extraction.Mime, ct);
            extraction.ExtractionMethod = doc.Method.ToString();
            log.LogInformation("Text extracted. method={Method} chars={Chars} ocr_conf={Conf} duration_ms={Ms}",
                doc.Method, doc.Text.Length, doc.Confidence, swRouter.ElapsedMilliseconds);

            var useVision = doc.PageImages is { Count: > 0 };

            LlmExtractionResult result;
            var swLlm = Stopwatch.StartNew();
            if (useVision && doc.PageImages is { Count: > 0 })
            {
                log.LogInformation("Calling LLM (vision). model={Model} pages={Pages}", settings.VisionModel, doc.PageImages.Count);
                progress?.Report(new ProgressEvent("calling_llm", $"vision:{settings.VisionModel} pages={doc.PageImages.Count}"));
                result = await llm.ExtractAsync(doc.Text, extraction.JsonSchemaSnapshot, settings.VisionModel, doc.PageImages, llmTimeout, settings.Think, settings.NumCtx, ct);
                extraction.ExtractionMethod = ExtractionMethod.Vision.ToString();
                extraction.Model = settings.VisionModel;
            }
            else
            {
                log.LogInformation("Calling LLM (text). model={Model}", effectiveModel);
                progress?.Report(new ProgressEvent("calling_llm", $"text:{effectiveModel}"));
                result = await llm.ExtractAsync(doc.Text, extraction.JsonSchemaSnapshot, effectiveModel, null, llmTimeout, settings.Think, settings.NumCtx, ct);
            }

            log.LogInformation("LLM done. validated={Valid} tokens_in={In} tokens_out={Out} duration_ms={Ms}",
                result.Validated, result.TokensIn, result.TokensOut, swLlm.ElapsedMilliseconds);
            progress?.Report(new ProgressEvent("validating_schema", $"tokens={result.TokensIn}→{result.TokensOut}"));

            extraction.Result = result.Json;
            extraction.ExtractedText = Truncate(doc.Text, 200_000);
            extraction.TokensIn = result.TokensIn;
            extraction.TokensOut = result.TokensOut;
            extraction.Status = result.Validated ? ExtractionStatus.Succeeded : ExtractionStatus.Failed;
            extraction.Error = result.Validated ? null : $"Schema validation failed: {result.ValidationError}";
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Extraction failed");
            extraction.Status = ExtractionStatus.Failed;
            extraction.Error = ex.Message;
        }

        extraction.FinishedAt = DateTimeOffset.UtcNow;
        extraction.AttemptCount++;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Extraction finished. status={Status} total_ms={Ms}", extraction.Status, totalSw.ElapsedMilliseconds);
        progress?.Report(new ProgressEvent(
            extraction.Status == ExtractionStatus.Succeeded ? "done" : "failed",
            extraction.Error,
            totalSw.ElapsedMilliseconds));

        _ = Task.Run(() => webhooks.DispatchAsync(extraction, CancellationToken.None), CancellationToken.None);

        return extraction;
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max];
}

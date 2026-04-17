using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Jobs;
using ScribAi.Api.Options;
using ScribAi.Api.Pipeline;
using ScribAi.Api.Pipeline.Llm;
using ScribAi.Api.Storage;

namespace ScribAi.Api.Services;

public class ExtractionService(
    ScribaiDbContext db,
    IDocumentRouter router,
    IOllamaExtractor llm,
    IBlobStore blobs,
    WebhookDispatcher webhooks,
    ITenantSettingsService tenantSettings,
    IOptions<ProcessingOptions> procOpt,
    ILogger<ExtractionService> log)
{
    public async Task<Extraction> ProcessAsync(Extraction extraction, Stream content, bool storeOriginal, string model, CancellationToken ct)
    {
        var settings = await tenantSettings.GetAsync(extraction.TenantId, ct);
        var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model : settings.DefaultTextModel;
        var llmTimeout = TimeSpan.FromSeconds(settings.OllamaTimeoutSeconds);

        extraction.Status = ExtractionStatus.Running;
        extraction.StartedAt = DateTimeOffset.UtcNow;
        extraction.Model = effectiveModel;
        await db.SaveChangesAsync(ct);

        try
        {
            if (storeOriginal)
            {
                content.Position = 0;
                extraction.StorageKey = await blobs.PutAsync(content, extraction.SourceFilename, extraction.Mime, ct);
                content.Position = 0;
            }

            var doc = await router.ExtractAsync(content, extraction.SourceFilename, extraction.Mime, ct);
            extraction.ExtractionMethod = doc.Method.ToString();

            var useVision = (doc.Method == ExtractionMethod.PdfOcr) ||
                            (doc.Confidence is { } c && c < procOpt.Value.OcrConfidenceThreshold);

            LlmExtractionResult result;
            if (useVision && doc.PageImages is { Count: > 0 })
            {
                log.LogInformation("Using vision model {Model} (ocr_conf={Conf})", settings.VisionModel, doc.Confidence);
                result = await llm.ExtractAsync(doc.Text, extraction.JsonSchemaSnapshot, settings.VisionModel, doc.PageImages, llmTimeout, ct);
                extraction.ExtractionMethod = ExtractionMethod.Vision.ToString();
                extraction.Model = settings.VisionModel;
            }
            else
            {
                result = await llm.ExtractAsync(doc.Text, extraction.JsonSchemaSnapshot, effectiveModel, null, llmTimeout, ct);
            }

            extraction.Result = result.Json;
            extraction.ExtractedText = Truncate(doc.Text, 200_000);
            extraction.TokensIn = result.TokensIn;
            extraction.TokensOut = result.TokensOut;
            extraction.Status = result.Validated ? ExtractionStatus.Succeeded : ExtractionStatus.Failed;
            extraction.Error = result.Validated ? null : $"Schema validation failed: {result.ValidationError}";
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Extraction {Id} failed", extraction.Id);
            extraction.Status = ExtractionStatus.Failed;
            extraction.Error = ex.Message;
        }

        extraction.FinishedAt = DateTimeOffset.UtcNow;
        extraction.AttemptCount++;
        await db.SaveChangesAsync(ct);

        _ = Task.Run(() => webhooks.DispatchAsync(extraction, CancellationToken.None), CancellationToken.None);

        return extraction;
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max];
}

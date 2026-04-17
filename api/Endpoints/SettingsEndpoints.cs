using ScribAi.Api.Auth;
using ScribAi.Api.Services;
using ScribAi.Api.Endpoints; // IAuditLogger already via Services
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScribAi.Api.Endpoints;

public static class SettingsEndpoints
{
    public record SettingsDto(
        string? DefaultTextModel,
        string? VisionModel,
        int? OllamaTimeoutSeconds,
        int? WebhookMaxAttempts,
        int? WebhookTimeoutSeconds,
        ResolvedTenantSettings Effective
    );

    public record SettingsUpdateRequest(
        string? DefaultTextModel,
        string? VisionModel,
        int? OllamaTimeoutSeconds,
        int? WebhookMaxAttempts,
        int? WebhookTimeoutSeconds,
        bool ClearDefaultTextModel = false,
        bool ClearVisionModel = false,
        bool ClearOllamaTimeoutSeconds = false,
        bool ClearWebhookMaxAttempts = false,
        bool ClearWebhookTimeoutSeconds = false
    );

    public record ModelInfo(string Name, long? Size);
    public record TestModelRequest(string Model);
    public record TestModelResult(bool Ok, string? Response, string? Error, long ElapsedMs);

    public static void MapSettings(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/settings").WithTags("Settings");

        g.MapGet("/", async (HttpContext ctx, ITenantSettingsService svc, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();
            var raw = await svc.GetRawAsync(t.TenantId, ct);
            var eff = await svc.GetAsync(t.TenantId, ct);
            return Results.Ok(new SettingsDto(
                raw.DefaultTextModel, raw.VisionModel, raw.OllamaTimeoutSeconds,
                raw.WebhookMaxAttempts, raw.WebhookTimeoutSeconds, eff));
        });

        g.MapPut("/", async (SettingsUpdateRequest req, HttpContext ctx, ITenantSettingsService svc, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();

            await svc.UpdateAsync(t.TenantId, s =>
            {
                if (req.ClearDefaultTextModel) s.DefaultTextModel = null;
                else if (req.DefaultTextModel is not null) s.DefaultTextModel = req.DefaultTextModel.Trim();

                if (req.ClearVisionModel) s.VisionModel = null;
                else if (req.VisionModel is not null) s.VisionModel = req.VisionModel.Trim();

                if (req.ClearOllamaTimeoutSeconds) s.OllamaTimeoutSeconds = null;
                else if (req.OllamaTimeoutSeconds is int t1) s.OllamaTimeoutSeconds = Math.Clamp(t1, 5, 3600);

                if (req.ClearWebhookMaxAttempts) s.WebhookMaxAttempts = null;
                else if (req.WebhookMaxAttempts is int w1) s.WebhookMaxAttempts = Math.Clamp(w1, 1, 20);

                if (req.ClearWebhookTimeoutSeconds) s.WebhookTimeoutSeconds = null;
                else if (req.WebhookTimeoutSeconds is int w2) s.WebhookTimeoutSeconds = Math.Clamp(w2, 1, 300);
            }, ct);

            var raw = await svc.GetRawAsync(t.TenantId, ct);
            var eff = await svc.GetAsync(t.TenantId, ct);
            await audit.LogAsync(ctx, "tenant_settings.updated", target: t.TenantId.ToString(), details: req, ct: ct);
            return Results.Ok(new SettingsDto(
                raw.DefaultTextModel, raw.VisionModel, raw.OllamaTimeoutSeconds,
                raw.WebhookMaxAttempts, raw.WebhookTimeoutSeconds, eff));
        });

        g.MapGet("/models", async (HttpContext ctx, IHttpClientFactory http, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();
            try
            {
                using var client = http.CreateClient("ollama-meta");
                var json = await client.GetStringAsync("/api/tags", ct);
                using var doc = JsonDocument.Parse(json);
                var list = new List<ModelInfo>();
                if (doc.RootElement.TryGetProperty("models", out var arr))
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var name = el.GetProperty("name").GetString() ?? "";
                        long? size = el.TryGetProperty("size", out var sz) ? sz.GetInt64() : null;
                        list.Add(new ModelInfo(name, size));
                    }
                }
                return Results.Ok(list);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Cannot reach Ollama: {ex.Message}", statusCode: 502);
            }
        });

        g.MapPost("/models/test", async (TestModelRequest req, HttpContext ctx, IHttpClientFactory http, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Model)) return Results.BadRequest(new { error = "model_required" });

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var client = http.CreateClient("ollama-meta");
                client.Timeout = TimeSpan.FromSeconds(60);
                var payload = new JsonObject
                {
                    ["model"] = req.Model,
                    ["prompt"] = "Reply with exactly the word: OK",
                    ["stream"] = false,
                    ["options"] = new JsonObject { ["temperature"] = 0 }
                };
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
                {
                    Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
                };
                using var resp = await client.SendAsync(httpReq, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                sw.Stop();

                if (!resp.IsSuccessStatusCode)
                    return Results.Ok(new TestModelResult(false, null, body, sw.ElapsedMilliseconds));

                using var doc = JsonDocument.Parse(body);
                var response = doc.RootElement.TryGetProperty("response", out var r) ? r.GetString() : null;
                return Results.Ok(new TestModelResult(true, response, null, sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Results.Ok(new TestModelResult(false, null, ex.Message, sw.ElapsedMilliseconds));
            }
        });
    }
}

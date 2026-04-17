using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScribAi.Api.Jobs;

public class WebhookDispatcher(
    HttpClient http,
    IDbContextFactory<ScribaiDbContext> dbFactory,
    ITenantSettingsService tenantSettings,
    ILogger<WebhookDispatcher> log)
{
    public async Task DispatchAsync(Extraction ext, CancellationToken ct)
    {
        var cfg = await tenantSettings.GetAsync(ext.TenantId, ct);
        var maxAttempts = cfg.WebhookMaxAttempts;
        var deliveryTimeout = TimeSpan.FromSeconds(cfg.WebhookTimeoutSeconds);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var evt = ext.Status == ExtractionStatus.Succeeded ? "extraction.succeeded" : "extraction.failed";

        var hooks = await db.Webhooks
            .Where(w => w.TenantId == ext.TenantId && w.Active && w.Events.Contains(evt)
                        && (w.ApiKeyId == null || w.ApiKeyId == ext.ApiKeyId))
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(ext.WebhookUrl))
        {
            hooks.Add(new Webhook { Url = ext.WebhookUrl, Secret = string.Empty, Events = [evt], Active = true });
        }

        foreach (var hook in hooks)
        {
            await TryDeliverAsync(hook, ext, evt, db, maxAttempts, deliveryTimeout, ct);
        }
    }

    private async Task TryDeliverAsync(Webhook hook, Extraction ext, string evt, ScribaiDbContext db,
        int maxAttempts, TimeSpan deliveryTimeout, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = ext.Id,
            tenantId = ext.TenantId,
            status = ext.Status.ToString().ToLowerInvariant(),
            @event = evt,
            result = ext.Result is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(ext.Result),
            error = ext.Error,
            createdAt = ext.CreatedAt,
            finishedAt = ext.FinishedAt
        });

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var delivery = new WebhookDelivery
            {
                WebhookId = hook.Id,
                ExtractionId = ext.Id,
                Event = evt,
                Attempt = attempt
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, hook.Url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                if (!string.IsNullOrEmpty(hook.Secret))
                {
                    var sig = ComputeHmac(hook.Secret, payload);
                    req.Headers.Add("X-Scribai-Signature", sig);
                }
                req.Headers.Add("X-Scribai-Event", evt);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(deliveryTimeout);
                using var resp = await http.SendAsync(req, cts.Token);
                delivery.StatusCode = (int)resp.StatusCode;
                var body = await resp.Content.ReadAsStringAsync(ct);
                delivery.Response = body.Length > 2000 ? body[..2000] : body;

                if (resp.IsSuccessStatusCode)
                {
                    delivery.DeliveredAt = DateTimeOffset.UtcNow;
                    if (hook.Id != Guid.Empty) db.WebhookDeliveries.Add(delivery);
                    await db.SaveChangesAsync(ct);
                    return;
                }
            }
            catch (Exception ex)
            {
                delivery.Error = ex.Message;
                log.LogWarning(ex, "Webhook delivery attempt {Attempt} failed to {Url}", attempt, hook.Url);
            }

            if (hook.Id != Guid.Empty) db.WebhookDeliveries.Add(delivery);
            await db.SaveChangesAsync(ct);

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }
    }

    private static string ComputeHmac(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexStringLower(sig);
    }
}

using ScribAi.Api.Auth;
using ScribAi.Api.Security;
using ScribAi.Api.Services;
using Serilog;

namespace ScribAi.Api.Endpoints;

public static class AdminGlobalEndpoints
{
    public record GlobalDto(
        bool SeqEnabled,
        string? SeqUrl,
        bool HasSeqApiKey,
        string SeqMinimumLevel,
        string ApplicationName,
        string[] AllowedOrigins,
        bool AllowAnyOrigin,
        string? OllamaBaseUrl,
        string EffectiveOllamaBaseUrl,
        DateTimeOffset UpdatedAt
    );

    public record GlobalUpdateRequest(
        bool SeqEnabled,
        string? SeqUrl,
        string? SeqApiKey,
        bool ClearSeqApiKey,
        string SeqMinimumLevel,
        string? ApplicationName,
        string[]? AllowedOrigins,
        bool? AllowAnyOrigin,
        string? OllamaBaseUrl,
        bool ClearOllamaBaseUrl = false
    );

    public record SeqTestResult(bool Ok, string? Error);

    public static void MapAdminGlobal(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/admin/global").WithTags("AdminGlobal");

        g.MapGet("/", async (HttpContext ctx, IGlobalSettingsProvider provider, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();
            var row = await provider.GetRawAsync(ct);
            return Results.Ok(new GlobalDto(
                row.SeqEnabled, row.SeqUrl, row.SeqApiKeyEncrypted is { Length: > 0 },
                row.SeqMinimumLevel, row.ApplicationName,
                row.AllowedOrigins ?? [], row.AllowAnyOrigin,
                row.OllamaBaseUrl, provider.Current.OllamaBaseUrl,
                row.UpdatedAt));
        });

        g.MapPut("/", async (GlobalUpdateRequest req, HttpContext ctx, IGlobalSettingsProvider provider, ISecretsProtector secrets, IAuditLogger audit, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();

            if (req.SeqEnabled && string.IsNullOrWhiteSpace(req.SeqUrl))
                return Results.BadRequest(new { error = "seq_url_required_when_enabled" });

            if (!string.IsNullOrWhiteSpace(req.SeqUrl) && !Uri.TryCreate(req.SeqUrl, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "invalid_seq_url" });

            if (!req.ClearOllamaBaseUrl && !string.IsNullOrWhiteSpace(req.OllamaBaseUrl) &&
                !Uri.TryCreate(req.OllamaBaseUrl, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "invalid_ollama_base_url" });

            var levels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };
            var level = levels.FirstOrDefault(l => l.Equals(req.SeqMinimumLevel, StringComparison.OrdinalIgnoreCase))
                        ?? "Information";

            await provider.UpdateAsync(row =>
            {
                row.SeqEnabled = req.SeqEnabled;
                row.SeqUrl = string.IsNullOrWhiteSpace(req.SeqUrl) ? null : req.SeqUrl.TrimEnd('/');
                row.SeqMinimumLevel = level;
                if (!string.IsNullOrWhiteSpace(req.ApplicationName))
                    row.ApplicationName = req.ApplicationName.Trim();

                if (req.AllowedOrigins is not null)
                    row.AllowedOrigins = req.AllowedOrigins
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Select(o => o.Trim().TrimEnd('/'))
                        .ToArray();
                if (req.AllowAnyOrigin is bool aao) row.AllowAnyOrigin = aao;

                if (req.ClearSeqApiKey) row.SeqApiKeyEncrypted = null;
                else if (!string.IsNullOrWhiteSpace(req.SeqApiKey))
                    row.SeqApiKeyEncrypted = secrets.Encrypt(req.SeqApiKey);

                if (req.ClearOllamaBaseUrl) row.OllamaBaseUrl = null;
                else if (!string.IsNullOrWhiteSpace(req.OllamaBaseUrl))
                    row.OllamaBaseUrl = req.OllamaBaseUrl.Trim().TrimEnd('/');
            }, t.ApiKeyId, ct);

            var fresh = await provider.GetRawAsync(ct);
            await audit.LogAsync(ctx, "global_settings.updated", target: "global",
                details: new { req.SeqEnabled, req.SeqUrl, req.ClearSeqApiKey, req.SeqMinimumLevel, req.ApplicationName, req.AllowedOrigins, req.AllowAnyOrigin, req.OllamaBaseUrl, req.ClearOllamaBaseUrl, keyChanged = !string.IsNullOrWhiteSpace(req.SeqApiKey) },
                ct: ct);
            return Results.Ok(new GlobalDto(
                fresh.SeqEnabled, fresh.SeqUrl, fresh.SeqApiKeyEncrypted is { Length: > 0 },
                fresh.SeqMinimumLevel, fresh.ApplicationName,
                fresh.AllowedOrigins ?? [], fresh.AllowAnyOrigin,
                fresh.OllamaBaseUrl, provider.Current.OllamaBaseUrl,
                fresh.UpdatedAt));
        });

        g.MapPost("/seq/test", (HttpContext ctx, IGlobalSettingsProvider provider) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();
            var cur = provider.Current;
            if (!cur.SeqEnabled || string.IsNullOrWhiteSpace(cur.SeqUrl))
                return Results.BadRequest(new SeqTestResult(false, "Seq disabled or URL empty"));

            try
            {
                Log.ForContext("SeqTest", true)
                   .ForContext("TenantId", t.TenantId)
                   .ForContext("ApiKeyId", t.ApiKeyId)
                   .Information("ScribAi Seq test OK from tenant {Tenant}", t.TenantId);
                return Results.Ok(new SeqTestResult(true, null));
            }
            catch (Exception ex)
            {
                return Results.Ok(new SeqTestResult(false, ex.Message));
            }
        });
    }
}

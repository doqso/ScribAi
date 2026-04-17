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
        DateTimeOffset UpdatedAt
    );

    public record GlobalUpdateRequest(
        bool SeqEnabled,
        string? SeqUrl,
        string? SeqApiKey,
        bool ClearSeqApiKey,
        string SeqMinimumLevel,
        string? ApplicationName
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
                row.SeqMinimumLevel, row.ApplicationName, row.UpdatedAt));
        });

        g.MapPut("/", async (GlobalUpdateRequest req, HttpContext ctx, IGlobalSettingsProvider provider, ISecretsProtector secrets, CancellationToken ct) =>
        {
            var t = ctx.Tenant();
            if (!t.IsAdmin) return Results.Forbid();

            if (req.SeqEnabled && string.IsNullOrWhiteSpace(req.SeqUrl))
                return Results.BadRequest(new { error = "seq_url_required_when_enabled" });

            if (!string.IsNullOrWhiteSpace(req.SeqUrl) && !Uri.TryCreate(req.SeqUrl, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "invalid_seq_url" });

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

                if (req.ClearSeqApiKey) row.SeqApiKeyEncrypted = null;
                else if (!string.IsNullOrWhiteSpace(req.SeqApiKey))
                    row.SeqApiKeyEncrypted = secrets.Encrypt(req.SeqApiKey);
            }, t.ApiKeyId, ct);

            var fresh = await provider.GetRawAsync(ct);
            return Results.Ok(new GlobalDto(
                fresh.SeqEnabled, fresh.SeqUrl, fresh.SeqApiKeyEncrypted is { Length: > 0 },
                fresh.SeqMinimumLevel, fresh.ApplicationName, fresh.UpdatedAt));
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

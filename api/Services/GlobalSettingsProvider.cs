using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Security;

namespace ScribAi.Api.Services;

public record ResolvedGlobalSettings(
    bool SeqEnabled,
    string? SeqUrl,
    string? SeqApiKey,
    string SeqMinimumLevel,
    string ApplicationName,
    string[] AllowedOrigins,
    bool AllowAnyOrigin
);

public interface IGlobalSettingsProvider
{
    ResolvedGlobalSettings Current { get; }
    event Action<ResolvedGlobalSettings>? Changed;
    Task ReloadAsync(CancellationToken ct = default);
    Task<GlobalSettings> GetRawAsync(CancellationToken ct = default);
    Task UpdateAsync(Action<GlobalSettings> mutate, Guid? updatedBy, CancellationToken ct = default);
}

public class GlobalSettingsProvider(
    IDbContextFactory<ScribaiDbContext> dbFactory,
    ISecretsProtector secrets,
    ILogger<GlobalSettingsProvider> log) : IGlobalSettingsProvider
{
    private ResolvedGlobalSettings _current = new(false, null, null, "Information", "ScribAi", [], false);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ResolvedGlobalSettings Current => _current;
    public event Action<ResolvedGlobalSettings>? Changed;

    public async Task<GlobalSettings> GetRawAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.GlobalSettings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new GlobalSettings { Id = 1 };
            db.GlobalSettings.Add(row);
            await db.SaveChangesAsync(ct);
        }
        return row;
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var row = await GetRawAsync(ct);
            string? plainKey = null;
            if (row.SeqApiKeyEncrypted is { Length: > 0 })
            {
                try { plainKey = secrets.Decrypt(row.SeqApiKeyEncrypted); }
                catch (Exception ex) { log.LogError(ex, "Failed to decrypt Seq API key"); }
            }
            _current = new ResolvedGlobalSettings(
                SeqEnabled: row.SeqEnabled,
                SeqUrl: row.SeqUrl,
                SeqApiKey: plainKey,
                SeqMinimumLevel: row.SeqMinimumLevel,
                ApplicationName: string.IsNullOrWhiteSpace(row.ApplicationName) ? "ScribAi" : row.ApplicationName,
                AllowedOrigins: row.AllowedOrigins ?? [],
                AllowAnyOrigin: row.AllowAnyOrigin);

            log.LogInformation("Global settings reloaded: seq_enabled={Enabled} url={Url}", _current.SeqEnabled, _current.SeqUrl);
            Changed?.Invoke(_current);
        }
        finally { _lock.Release(); }
    }

    public async Task UpdateAsync(Action<GlobalSettings> mutate, Guid? updatedBy, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.GlobalSettings.FirstOrDefaultAsync(ct)
            ?? db.GlobalSettings.Add(new GlobalSettings { Id = 1 }).Entity;
        mutate(row);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = updatedBy;
        await db.SaveChangesAsync(ct);
        await ReloadAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;
using ScribAi.Api.Options;
using ScribAi.Api.Services;
using ScribAi.Api.Storage;
using StackExchange.Redis;

namespace ScribAi.Api.Jobs;

public class ExtractionWorker(
    IConnectionMultiplexer redis,
    IServiceProvider sp,
    IOptions<RedisOptions> opt,
    ILogger<ExtractionWorker> log) : BackgroundService
{
    private readonly RedisOptions _opt = opt.Value;
    private readonly string _consumerName = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var dbFromRedis = redis.GetDatabase();
        await EnsureGroupAsync(dbFromRedis);

        log.LogInformation("ExtractionWorker started as {Consumer}", _consumerName);

        while (!stop.IsCancellationRequested)
        {
            try
            {
                var entries = await dbFromRedis.StreamReadGroupAsync(
                    _opt.StreamKey, _opt.ConsumerGroup, _consumerName, count: 4, noAck: false);

                if (entries.Length == 0)
                {
                    await Task.Delay(1000, stop);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await HandleAsync(entry, stop);
                    await dbFromRedis.StreamAcknowledgeAsync(_opt.StreamKey, _opt.ConsumerGroup, entry.Id);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                log.LogError(ex, "Worker loop error");
                await Task.Delay(2000, stop);
            }
        }
    }

    private async Task EnsureGroupAsync(IDatabase db)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(_opt.StreamKey, _opt.ConsumerGroup, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // already exists
        }
    }

    private async Task HandleAsync(StreamEntry entry, CancellationToken ct)
    {
        var extractionId = Guid.Parse((string)entry.Values.First(v => v.Name == "extractionId").Value!);
        log.LogInformation("Processing extraction {Id}", extractionId);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ScribaiDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<ExtractionService>();
        var blobs = scope.ServiceProvider.GetRequiredService<IBlobStore>();

        var extraction = await db.Extractions.FirstOrDefaultAsync(e => e.Id == extractionId, ct);
        if (extraction is null)
        {
            log.LogWarning("Extraction {Id} not found; skipping", extractionId);
            return;
        }

        if (string.IsNullOrEmpty(extraction.StorageKey))
        {
            extraction.Status = ExtractionStatus.Failed;
            extraction.Error = "Async job requires stored original but storage_key is empty";
            extraction.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        await using var content = await blobs.GetAsync(extraction.StorageKey, ct);
        await svc.ProcessAsync(extraction, content, storeOriginal: false, extraction.Model, ct);
    }
}

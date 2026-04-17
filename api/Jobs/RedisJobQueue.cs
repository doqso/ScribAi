using Microsoft.Extensions.Options;
using ScribAi.Api.Options;
using StackExchange.Redis;

namespace ScribAi.Api.Jobs;

public class RedisJobQueue(IConnectionMultiplexer redis, IOptions<RedisOptions> opt) : IJobQueue
{
    private readonly RedisOptions _opt = opt.Value;

    public async Task EnqueueAsync(ExtractionJob job, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StreamAddAsync(_opt.StreamKey,
        [
            new NameValueEntry("extractionId", job.ExtractionId.ToString()),
            new NameValueEntry("tenantId", job.TenantId.ToString())
        ]);
    }
}

using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Data;
using StackExchange.Redis;

namespace ScribAi.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/readyz", async (ScribaiDbContext db, IConnectionMultiplexer redis, CancellationToken ct) =>
        {
            var dbOk = false;
            var redisOk = false;
            try { dbOk = await db.Database.CanConnectAsync(ct); } catch { }
            try { redisOk = redis.IsConnected; } catch { }

            var ok = dbOk && redisOk;
            return ok
                ? Results.Ok(new { db = dbOk, redis = redisOk })
                : Results.Json(new { db = dbOk, redis = redisOk }, statusCode: 503);
        });
    }
}

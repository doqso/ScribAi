using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Data;

namespace ScribAi.Api.Auth;

public class ApiKeyMiddleware(RequestDelegate next)
{
    private const string Header = "X-API-Key";

    public async Task InvokeAsync(HttpContext ctx, IDbContextFactory<ScribaiDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var path = ctx.Request.Path.Value ?? "";
        if (path.StartsWith("/healthz") || path.StartsWith("/readyz") || path.StartsWith("/openapi") || path.StartsWith("/v1/bootstrap"))
        {
            await next(ctx);
            return;
        }

        if (!ctx.Request.Headers.TryGetValue(Header, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            await WriteUnauthorized(ctx, "missing_api_key");
            return;
        }

        var hash = ApiKeyHasher.Hash(provided.ToString());
        var key = await db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null);

        if (key is null)
        {
            await WriteUnauthorized(ctx, "invalid_api_key");
            return;
        }

        ctx.Items["TenantContext"] = TenantContext.From(key);

        var keyId = key.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var bg = await dbFactory.CreateDbContextAsync();
                await bg.ApiKeys
                    .Where(k => k.Id == keyId)
                    .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, DateTimeOffset.UtcNow));
            }
            catch { /* ignore */ }
        });

        await next(ctx);
    }

    private static Task WriteUnauthorized(HttpContext ctx, string code)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync($"{{\"error\":\"{code}\"}}");
    }
}

public static class HttpContextExtensions
{
    public static TenantContext Tenant(this HttpContext ctx) =>
        (TenantContext)(ctx.Items["TenantContext"]
            ?? throw new InvalidOperationException("TenantContext not set; middleware order wrong"));
}

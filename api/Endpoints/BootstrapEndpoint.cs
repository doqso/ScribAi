using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Data.Entities;

namespace ScribAi.Api.Endpoints;

public static class BootstrapEndpoint
{
    public record BootstrapRequest(string TenantName, string KeyLabel);
    public record BootstrapResponse(Guid TenantId, Guid KeyId, string Key);

    public static void MapBootstrap(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/bootstrap", async (BootstrapRequest req, ScribaiDbContext db, CancellationToken ct) =>
        {
            if (await db.Tenants.AnyAsync(ct))
                return Results.Conflict(new { error = "already_bootstrapped" });

            if (string.IsNullOrWhiteSpace(req.TenantName) || string.IsNullOrWhiteSpace(req.KeyLabel))
                return Results.BadRequest(new { error = "invalid_request" });

            var tenant = new Tenant { Name = req.TenantName };
            var plain = ApiKeyHasher.Generate();
            var key = new ApiKey
            {
                TenantId = tenant.Id,
                Label = req.KeyLabel,
                KeyHash = ApiKeyHasher.Hash(plain),
                KeyPrefix = ApiKeyHasher.Prefix(plain),
                IsAdmin = true,
                StoreOriginals = true
            };
            db.Tenants.Add(tenant);
            db.ApiKeys.Add(key);
            db.TenantSettings.Add(new TenantSettings { TenantId = tenant.Id });
            await db.SaveChangesAsync(ct);

            return Results.Ok(new BootstrapResponse(tenant.Id, key.Id, plain));
        }).WithTags("Bootstrap").AllowAnonymous();
    }
}

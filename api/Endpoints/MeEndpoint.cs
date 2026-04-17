using ScribAi.Api.Auth;

namespace ScribAi.Api.Endpoints;

public static class MeEndpoint
{
    public record MeDto(Guid TenantId, Guid ApiKeyId, bool IsAdmin, bool StoreOriginals, string DefaultModel);

    public static void MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/me", (HttpContext ctx) =>
        {
            var t = ctx.Tenant();
            return Results.Ok(new MeDto(t.TenantId, t.ApiKeyId, t.IsAdmin, t.StoreOriginals, t.DefaultModel));
        }).WithTags("Me");
    }
}

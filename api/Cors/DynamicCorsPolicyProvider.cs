using Microsoft.AspNetCore.Cors.Infrastructure;
using ScribAi.Api.Services;

namespace ScribAi.Api.Cors;

public class DynamicCorsPolicyProvider(IGlobalSettingsProvider settings) : ICorsPolicyProvider
{
    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var cfg = settings.Current;
        var builder = new CorsPolicyBuilder()
            .AllowAnyHeader()
            .AllowAnyMethod();

        if (cfg.AllowAnyOrigin || cfg.AllowedOrigins.Length == 0)
        {
            builder.AllowAnyOrigin();
        }
        else
        {
            builder.WithOrigins(cfg.AllowedOrigins);
        }

        return Task.FromResult<CorsPolicy?>(builder.Build());
    }
}

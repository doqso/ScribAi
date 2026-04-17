using ScribAi.Api.Auth;
using Serilog.Core;
using Serilog.Events;

namespace ScribAi.Api.Logging;

public class TenantEnricher : ILogEventEnricher
{
    private static IHttpContextAccessor? _accessor;

    public static void UseAccessor(IHttpContextAccessor accessor) => _accessor = accessor;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var ctx = _accessor?.HttpContext;
        if (ctx is null) return;
        if (ctx.Items["TenantContext"] is TenantContext t)
        {
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("TenantId", t.TenantId));
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("ApiKeyId", t.ApiKeyId));
        }
    }
}

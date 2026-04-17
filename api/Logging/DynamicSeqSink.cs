using ScribAi.Api.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ScribAi.Api.Logging;

public class DynamicSeqSink : ILogEventSink, IDisposable
{
    private readonly object _lock = new();
    private IGlobalSettingsProvider? _provider;
    private Logger? _innerLogger;

    public void Attach(IGlobalSettingsProvider provider)
    {
        _provider = provider;
        provider.Changed += OnChanged;
        Rebuild(provider.Current);
    }

    private void OnChanged(ResolvedGlobalSettings cfg) => Rebuild(cfg);

    private void Rebuild(ResolvedGlobalSettings cfg)
    {
        lock (_lock)
        {
            try { _innerLogger?.Dispose(); } catch { }
            _innerLogger = null;

            if (!cfg.SeqEnabled || string.IsNullOrWhiteSpace(cfg.SeqUrl))
                return;

            var level = ParseLevel(cfg.SeqMinimumLevel);
            var lc = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .Enrich.WithProperty("Application", cfg.ApplicationName)
                .WriteTo.Seq(cfg.SeqUrl, apiKey: string.IsNullOrWhiteSpace(cfg.SeqApiKey) ? null : cfg.SeqApiKey);
            _innerLogger = lc.CreateLogger();
        }
    }

    public void Emit(LogEvent logEvent)
    {
        Logger? inner;
        lock (_lock) { inner = _innerLogger; }
        inner?.Write(logEvent);
    }

    private static LogEventLevel ParseLevel(string s) =>
        Enum.TryParse<LogEventLevel>(s, true, out var l) ? l : LogEventLevel.Information;

    public void Dispose()
    {
        if (_provider is not null) _provider.Changed -= OnChanged;
        lock (_lock)
        {
            _innerLogger?.Dispose();
            _innerLogger = null;
        }
    }
}

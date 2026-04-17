using ScribAi.Api.Services;

namespace ScribAi.Api.Logging;

/// <summary>Singleton holder so the Serilog sink can be created before DI is ready.</summary>
public static class DynamicSeqSinkHolder
{
    public static DynamicSeqSink Instance { get; } = new DynamicSeqSink();
}

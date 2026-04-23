namespace ScribAi.Api.Pipeline.Llm;

public record LlmExtractionResult(
    string Json,
    bool Validated,
    string? ValidationError,
    int? TokensIn,
    int? TokensOut,
    string Model
);

public interface IOllamaExtractor
{
    Task<LlmExtractionResult> ExtractAsync(
        string text,
        string jsonSchema,
        string model,
        IReadOnlyList<byte[]>? images = null,
        TimeSpan? perRequestTimeout = null,
        bool? think = null,
        int? numCtx = null,
        CancellationToken ct = default);
}

using Microsoft.Extensions.Options;
using NJsonSchema;
using ScribAi.Api.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScribAi.Api.Pipeline.Llm;

public class OllamaExtractor(HttpClient http, IOptions<OllamaOptions> opt, ILogger<OllamaExtractor> log) : IOllamaExtractor
{
    private readonly OllamaOptions _opt = opt.Value;

    private const string SystemPrompt =
        "You are a strict document data extractor. Given a document's text content and a JSON Schema, " +
        "you must extract fields and respond ONLY with a valid JSON object matching the schema exactly. " +
        "If a field is not present in the document, use null or omit it per schema rules. Do not invent values.";

    public async Task<LlmExtractionResult> ExtractAsync(
        string text,
        string jsonSchema,
        string model,
        IReadOnlyList<byte[]>? images = null,
        TimeSpan? perRequestTimeout = null,
        CancellationToken ct = default)
    {
        model = string.IsNullOrWhiteSpace(model) ? _opt.DefaultModel : model;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (perRequestTimeout is { } t && t > TimeSpan.Zero) linkedCts.CancelAfter(t);
        ct = linkedCts.Token;

        var schemaNode = JsonNode.Parse(jsonSchema)
            ?? throw new ArgumentException("Invalid JSON Schema", nameof(jsonSchema));

        var validator = await JsonSchema.FromJsonAsync(jsonSchema, ct);

        var attempt = 0;
        string? lastError = null;
        string lastJson = string.Empty;
        int? tokensIn = null, tokensOut = null;

        while (attempt < 2)
        {
            attempt++;
            var userContent = BuildUser(text, jsonSchema, lastError);

            var payload = new JsonObject
            {
                ["model"] = model,
                ["stream"] = false,
                ["format"] = schemaNode.DeepClone(),
                ["options"] = new JsonObject
                {
                    ["temperature"] = _opt.Temperature
                },
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = SystemPrompt },
                    BuildUserMessage(userContent, images)
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
            };

            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                log.LogError("Ollama error {Status}: {Body}", resp.StatusCode, body);
                throw new InvalidOperationException($"Ollama {(int)resp.StatusCode}: {body}");
            }

            using var parsed = JsonDocument.Parse(body);
            var root = parsed.RootElement;

            lastJson = root.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            tokensIn = root.TryGetProperty("prompt_eval_count", out var pec) ? pec.GetInt32() : null;
            tokensOut = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : null;

            var errors = validator.Validate(lastJson);
            if (errors.Count == 0)
                return new LlmExtractionResult(lastJson, true, null, tokensIn, tokensOut, model);

            lastError = string.Join("; ", errors.Select(e => $"{e.Path}: {e.Kind}"));
            log.LogWarning("LLM attempt {Attempt} schema invalid: {Err}", attempt, lastError);
        }

        return new LlmExtractionResult(lastJson, false, lastError, tokensIn, tokensOut, model);
    }

    private static JsonObject BuildUserMessage(string content, IReadOnlyList<byte[]>? images)
    {
        var msg = new JsonObject { ["role"] = "user", ["content"] = content };
        if (images is { Count: > 0 })
        {
            var arr = new JsonArray();
            foreach (var img in images) arr.Add(Convert.ToBase64String(img));
            msg["images"] = arr;
        }
        return msg;
    }

    private static string BuildUser(string text, string schema, string? previousError)
    {
        var sb = new StringBuilder();
        sb.AppendLine("JSON Schema to produce:");
        sb.AppendLine(schema);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(previousError))
        {
            sb.AppendLine("Previous attempt failed validation with these errors. Fix and retry:");
            sb.AppendLine(previousError);
            sb.AppendLine();
        }
        sb.AppendLine("Document content:");
        sb.AppendLine(text);
        return sb.ToString();
    }
}

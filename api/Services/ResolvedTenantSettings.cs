namespace ScribAi.Api.Services;

public record ResolvedTenantSettings(
    Guid TenantId,
    string DefaultTextModel,
    string VisionModel,
    int OllamaTimeoutSeconds,
    int WebhookMaxAttempts,
    int WebhookTimeoutSeconds,
    bool? Think,
    bool OcrEnabled
);

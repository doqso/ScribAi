namespace ScribAi.Api.Options;

public class OllamaOptions
{
    public const string Section = "Ollama";
    public string BaseUrl { get; set; } = "http://host.docker.internal:11434";
    public string DefaultModel { get; set; } = "qwen2.5:7b-instruct";
    public string VisionModel { get; set; } = "llama3.2-vision";
    public int TimeoutSeconds { get; set; } = 300;
    public double Temperature { get; set; } = 0;
}

namespace ScribAi.Api.Options;

public class RedisOptions
{
    public const string Section = "Redis";
    public string ConnectionString { get; set; } = "redis:6379";
    public string StreamKey { get; set; } = "scribai:extractions";
    public string ConsumerGroup { get; set; } = "workers";
}

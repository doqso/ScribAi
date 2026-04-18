namespace ScribAi.Api.Options;

public class ProcessingOptions
{
    public const string Section = "Processing";
    public long SyncMaxBytes { get; set; } = 2 * 1024 * 1024;
    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;
    public string TesseractLangs { get; set; } = "spa+eng";
    public string TesseractDataPath { get; set; } = "./tessdata";
    public int WebhookMaxAttempts { get; set; } = 5;
}

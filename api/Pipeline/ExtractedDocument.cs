namespace ScribAi.Api.Pipeline;

public enum ExtractionMethod
{
    PdfText,
    PdfOcr,
    Image,
    Office,
    Email,
    PlainText,
    Vision
}

public record ExtractedDocument(
    string Text,
    ExtractionMethod Method,
    double? Confidence = null,
    IReadOnlyList<byte[]>? PageImages = null
);

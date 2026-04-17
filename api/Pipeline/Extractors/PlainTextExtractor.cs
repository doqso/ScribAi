using System.Text;

namespace ScribAi.Api.Pipeline.Extractors;

public class PlainTextExtractor
{
    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return new ExtractedDocument(text, ExtractionMethod.PlainText);
    }
}

using ScribAi.Api.Options;
using Microsoft.Extensions.Options;
using ScribAi.Api.Pipeline.Ocr;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ScribAi.Api.Pipeline.Extractors;

public class PdfExtractor(IOptions<ProcessingOptions> opt, ILogger<PdfExtractor> log)
{
    private readonly double _threshold = opt.Value.OcrConfidenceThreshold;

    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var doc = PdfDocument.Open(ms);
        var sb = new StringBuilder();
        var hasText = false;

        foreach (var page in doc.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText) && pageText.Trim().Length > 20)
            {
                hasText = true;
                sb.AppendLine(pageText);
            }
        }

        if (hasText)
        {
            log.LogInformation("PDF native text extracted, pages {Pages}", doc.NumberOfPages);
            return new ExtractedDocument(sb.ToString(), ExtractionMethod.PdfText);
        }

        log.LogWarning("PDF appears scanned — OCR fallback not fully wired (requires rasterizer like PDFium). Returning empty text + original stream for vision fallback.");
        ms.Position = 0;
        var pageImages = new List<byte[]> { ms.ToArray() };
        return new ExtractedDocument(string.Empty, ExtractionMethod.PdfOcr, Confidence: 0.0, PageImages: pageImages);
    }
}

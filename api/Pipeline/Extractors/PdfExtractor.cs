using Microsoft.Extensions.Options;
using PDFtoImage;
using ScribAi.Api.Options;
using ScribAi.Api.Pipeline.Ocr;
using SkiaSharp;
using System.Text;
using UglyToad.PdfPig;

namespace ScribAi.Api.Pipeline.Extractors;

public class PdfExtractor(ITesseractOcr ocr, IOptions<ProcessingOptions> opt, ILogger<PdfExtractor> log)
{
    private const int Dpi = 200;
    private const int MaxPagesForOcr = 20;

    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var doc = PdfDocument.Open(ms);
        var sb = new StringBuilder();
        var hasText = false;
        var totalPages = doc.NumberOfPages;

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
            log.LogInformation("PDF native text extracted. pages={Pages}", totalPages);
            return new ExtractedDocument(sb.ToString(), ExtractionMethod.PdfText);
        }

        log.LogInformation("PDF has no native text — rasterizing {Pages} page(s) at {Dpi}dpi for OCR", totalPages, Dpi);
        ms.Position = 0;

        var pagesToProcess = Math.Min(totalPages, MaxPagesForOcr);
        var pageImages = new List<byte[]>(pagesToProcess);
        var texts = new StringBuilder();
        var confidences = new List<double>();

        try
        {
            using var pdfStream = new MemoryStream(ms.ToArray());
            var options = new RenderOptions(Dpi: Dpi);
            var idx = 0;
            foreach (var bitmap in Conversion.ToImages(pdfStream, options: options))
            {
                ct.ThrowIfCancellationRequested();
                using var img = bitmap;
                using var skData = img.Encode(SKEncodedImageFormat.Png, 90);
                var bytes = skData.ToArray();
                pageImages.Add(bytes);

                var oc = ocr.Run(bytes);
                texts.AppendLine($"--- Page {++idx} ---");
                texts.AppendLine(oc.Text);
                confidences.Add(oc.Confidence);
                log.LogDebug("PDF page {Page} OCR conf={Conf} chars={Chars}", idx, oc.Confidence, oc.Text.Length);

                if (idx >= MaxPagesForOcr) break;
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "PDF rasterization failed — falling back to raw PDF bytes for vision model");
            pageImages.Clear();
            pageImages.Add(ms.ToArray());
            return new ExtractedDocument(string.Empty, ExtractionMethod.PdfOcr, Confidence: 0.0, PageImages: pageImages);
        }

        var minConf = confidences.Count > 0 ? confidences.Min() : 0.0;
        return new ExtractedDocument(texts.ToString(), ExtractionMethod.PdfOcr, Confidence: minConf, PageImages: pageImages);
    }
}

using ScribAi.Api.Pipeline.Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ScribAi.Api.Pipeline.Extractors;

public class ImageExtractor(ITesseractOcr ocr, ILogger<ImageExtractor> log)
{
    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var original = ms.ToArray();

        var preprocessed = Preprocess(original);
        var result = ocr.Run(preprocessed);
        log.LogInformation("Image OCR confidence {Conf}", result.Confidence);

        return new ExtractedDocument(
            result.Text,
            ExtractionMethod.Image,
            result.Confidence,
            PageImages: [original]);
    }

    private static byte[] Preprocess(byte[] input)
    {
        using var image = Image.Load<Rgba32>(input);
        image.Mutate(x => x
            .Grayscale()
            .Contrast(1.2f));
        using var outMs = new MemoryStream();
        image.SaveAsPng(outMs);
        return outMs.ToArray();
    }
}

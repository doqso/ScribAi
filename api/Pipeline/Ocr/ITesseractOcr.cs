namespace ScribAi.Api.Pipeline.Ocr;

public record OcrResult(string Text, double Confidence);

public interface ITesseractOcr
{
    OcrResult Run(byte[] image);
}

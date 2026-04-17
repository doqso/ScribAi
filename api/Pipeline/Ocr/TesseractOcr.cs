using Microsoft.Extensions.Options;
using ScribAi.Api.Options;
using System.Diagnostics;
using System.Globalization;

namespace ScribAi.Api.Pipeline.Ocr;

public class TesseractOcr(IOptions<ProcessingOptions> opt, ILogger<TesseractOcr> log) : ITesseractOcr
{
    private readonly string _langs = opt.Value.TesseractLangs;

    public OcrResult Run(byte[] image)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"ocr-{Guid.NewGuid():N}");
        var inPath = tmp + ".png";
        var outBase = tmp + "-out";
        File.WriteAllBytes(inPath, image);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tesseract",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(inPath);
            psi.ArgumentList.Add(outBase);
            psi.ArgumentList.Add("-l"); psi.ArgumentList.Add(_langs);
            psi.ArgumentList.Add("tsv");

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("tesseract not found on PATH");
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(120_000);

            if (proc.ExitCode != 0)
            {
                log.LogError("tesseract failed ({Code}): {Err}", proc.ExitCode, stderr);
                return new OcrResult(string.Empty, 0);
            }

            var tsvPath = outBase + ".tsv";
            if (!File.Exists(tsvPath)) return new OcrResult(string.Empty, 0);

            var (text, conf) = ParseTsv(tsvPath);
            log.LogDebug("OCR confidence {Conf} length {Len}", conf, text.Length);
            return new OcrResult(text, conf);
        }
        finally
        {
            TryDelete(inPath);
            TryDelete(outBase + ".tsv");
            TryDelete(outBase + ".txt");
        }
    }

    private static (string text, double confidence) ParseTsv(string path)
    {
        var lines = File.ReadAllLines(path);
        var words = new List<string>();
        double confSum = 0;
        int confCount = 0;
        string? lastLineKey = null;

        foreach (var line in lines.Skip(1))
        {
            var cols = line.Split('\t');
            if (cols.Length < 12) continue;
            var text = cols[11];
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!double.TryParse(cols[10], CultureInfo.InvariantCulture, out var c) || c < 0) continue;

            var key = $"{cols[1]}:{cols[2]}:{cols[3]}:{cols[4]}";
            if (key != lastLineKey && words.Count > 0) words.Add("\n");
            lastLineKey = key;
            words.Add(text);
            confSum += c;
            confCount++;
        }

        var full = string.Join(' ', words).Replace(" \n ", "\n");
        var mean = confCount > 0 ? confSum / confCount / 100.0 : 0;
        return (full, mean);
    }

    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
}

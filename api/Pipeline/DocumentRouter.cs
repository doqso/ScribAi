using ScribAi.Api.Pipeline.Extractors;

namespace ScribAi.Api.Pipeline;

public class DocumentRouter(
    PdfExtractor pdf,
    ImageExtractor image,
    OfficeExtractor office,
    EmailExtractor email,
    PlainTextExtractor text,
    ILogger<DocumentRouter> log) : IDocumentRouter
{
    public async Task<ExtractedDocument> ExtractAsync(Stream content, string filename, string mime, bool useOcr, CancellationToken ct)
    {
        if (!content.CanSeek)
        {
            var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            ms.Position = 0;
            content = ms;
        }

        mime = string.IsNullOrWhiteSpace(mime) ? DetectMime(content, filename) : mime;
        log.LogInformation("Routing {File} as {Mime} use_ocr={UseOcr}", filename, mime, useOcr);

        return mime switch
        {
            "application/pdf" => await pdf.ExtractAsync(content, useOcr, ct),
            var m when m.StartsWith("image/") => await image.ExtractAsync(content, useOcr, ct),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                or "application/vnd.ms-excel"
                or "application/msword" => await office.ExtractAsync(content, mime, ct),
            "message/rfc822" or "application/vnd.ms-outlook" => await email.ExtractAsync(content, mime, ct),
            "text/plain" or "text/csv" => await text.ExtractAsync(content, ct),
            _ => throw new NotSupportedException($"Unsupported MIME: {mime}")
        };
    }

    public string DetectMime(Stream content, string filename)
    {
        if (!content.CanSeek) throw new ArgumentException("Stream must be seekable", nameof(content));

        var pos = content.Position;
        Span<byte> head = stackalloc byte[16];
        var read = content.Read(head);
        content.Position = pos;

        var slice = head[..read];

        if (StartsWith(slice, [0x25, 0x50, 0x44, 0x46])) return "application/pdf";
        if (StartsWith(slice, [0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (StartsWith(slice, [0x89, 0x50, 0x4E, 0x47])) return "image/png";
        if (StartsWith(slice, [0x47, 0x49, 0x46, 0x38])) return "image/gif";
        if (StartsWith(slice, [0x42, 0x4D])) return "image/bmp";
        if (StartsWith(slice, [0x49, 0x49, 0x2A, 0x00]) || StartsWith(slice, [0x4D, 0x4D, 0x00, 0x2A])) return "image/tiff";
        if (StartsWith(slice, [0x50, 0x4B, 0x03, 0x04]))
        {
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            return ext switch
            {
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/zip"
            };
        }

        var e = Path.GetExtension(filename).ToLowerInvariant();
        return e switch
        {
            ".eml" => "message/rfc822",
            ".msg" => "application/vnd.ms-outlook",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> s, byte[] sig)
    {
        if (s.Length < sig.Length) return false;
        for (var i = 0; i < sig.Length; i++) if (s[i] != sig[i]) return false;
        return true;
    }
}

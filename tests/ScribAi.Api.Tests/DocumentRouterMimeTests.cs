using Microsoft.Extensions.Logging.Abstractions;
using ScribAi.Api.Pipeline;
using ScribAi.Api.Pipeline.Extractors;

namespace ScribAi.Api.Tests;

public class DocumentRouterMimeTests
{
    private static DocumentRouter NewRouter() => new(
        pdf: null!, image: null!, office: null!, email: null!, text: null!,
        log: NullLogger<DocumentRouter>.Instance);

    private static Stream StreamOf(params byte[] bytes) => new MemoryStream(bytes);

    [Fact]
    public void Detects_pdf_by_magic_bytes()
    {
        using var s = StreamOf(0x25, 0x50, 0x44, 0x46, 0x2D, 0x31);
        Assert.Equal("application/pdf", NewRouter().DetectMime(s, "x.pdf"));
    }

    [Fact]
    public void Detects_png_by_magic_bytes()
    {
        using var s = StreamOf(0x89, 0x50, 0x4E, 0x47, 0x0D);
        Assert.Equal("image/png", NewRouter().DetectMime(s, "x.png"));
    }

    [Fact]
    public void Detects_jpeg_by_magic_bytes()
    {
        using var s = StreamOf(0xFF, 0xD8, 0xFF, 0xE0);
        Assert.Equal("image/jpeg", NewRouter().DetectMime(s, "x.jpg"));
    }

    [Fact]
    public void Zip_with_xlsx_extension_detected_as_xlsx()
    {
        using var s = StreamOf(0x50, 0x4B, 0x03, 0x04);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            NewRouter().DetectMime(s, "sheet.xlsx"));
    }

    [Fact]
    public void Unknown_bytes_fallback_to_extension()
    {
        using var s = StreamOf(0x00, 0x00, 0x00, 0x00);
        Assert.Equal("text/plain", NewRouter().DetectMime(s, "note.txt"));
    }
}

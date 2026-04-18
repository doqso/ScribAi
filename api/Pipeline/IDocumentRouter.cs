namespace ScribAi.Api.Pipeline;

public interface IDocumentRouter
{
    Task<ExtractedDocument> ExtractAsync(Stream content, string filename, string mime, bool useOcr, CancellationToken ct);
    string DetectMime(Stream content, string filename);
}

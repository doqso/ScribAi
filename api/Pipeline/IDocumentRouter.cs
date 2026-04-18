namespace ScribAi.Api.Pipeline;

public interface IDocumentRouter
{
    Task<ExtractedDocument> ExtractAsync(Stream content, string filename, string mime, CancellationToken ct);
    string DetectMime(Stream content, string filename);
}

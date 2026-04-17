using MimeKit;
using System.Text;

namespace ScribAi.Api.Pipeline.Extractors;

public class EmailExtractor
{
    public async Task<ExtractedDocument> ExtractAsync(Stream content, string mime, CancellationToken ct)
    {
        var msg = await MimeMessage.LoadAsync(content, ct);
        var sb = new StringBuilder();
        sb.Append("From: ").AppendLine(msg.From.ToString());
        sb.Append("To: ").AppendLine(msg.To.ToString());
        sb.Append("Cc: ").AppendLine(msg.Cc.ToString());
        sb.Append("Subject: ").AppendLine(msg.Subject);
        sb.Append("Date: ").AppendLine(msg.Date.ToString("O"));
        sb.AppendLine();
        sb.AppendLine(msg.TextBody ?? msg.HtmlBody ?? string.Empty);
        return new ExtractedDocument(sb.ToString(), ExtractionMethod.Email);
    }
}

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace ScribAi.Api.Pipeline.Extractors;

public class OfficeExtractor
{
    public Task<ExtractedDocument> ExtractAsync(Stream content, string mime, CancellationToken ct)
    {
        var text = mime.Contains("wordprocessingml") ? ReadDocx(content) : ReadXlsx(content);
        return Task.FromResult(new ExtractedDocument(text, ExtractionMethod.Office));
    }

    private static string ReadDocx(Stream s)
    {
        using var doc = WordprocessingDocument.Open(s, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
            sb.AppendLine(para.InnerText);
        return sb.ToString();
    }

    private static string ReadXlsx(Stream s)
    {
        using var doc = SpreadsheetDocument.Open(s, false);
        var wbPart = doc.WorkbookPart;
        if (wbPart is null) return string.Empty;

        var shared = wbPart.SharedStringTablePart?.SharedStringTable;
        var sb = new StringBuilder();

        foreach (var sheet in wbPart.Workbook.Descendants<Sheet>())
        {
            if (sheet.Id?.Value is null) continue;
            var part = (WorksheetPart)wbPart.GetPartById(sheet.Id.Value);
            sb.Append("# ").AppendLine(sheet.Name?.Value ?? "sheet");
            foreach (var row in part.Worksheet.Descendants<Row>())
            {
                var cells = row.Descendants<Cell>()
                    .Select(c => CellText(c, shared));
                sb.AppendLine(string.Join("\t", cells));
            }
        }
        return sb.ToString();
    }

    private static string CellText(Cell c, SharedStringTable? shared)
    {
        var v = c.CellValue?.Text ?? c.InnerText;
        if (c.DataType?.Value == CellValues.SharedString && shared is not null && int.TryParse(v, out var idx))
            return shared.ElementAtOrDefault(idx)?.InnerText ?? string.Empty;
        return v ?? string.Empty;
    }
}

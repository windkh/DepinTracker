namespace DepinTracker.Infrastructure.Reports;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Renders a <see cref="ReportDocument"/> to a real .docx file via Open XML — the
/// same SDK we already use for XLSX, so no new dependency. Suitable when the
/// reader expects an editable Word document (some Finanzamt offices and tax
/// advisors prefer Word over HTML/PDF for review).
/// </summary>
public sealed class WordReportExporter : IReportExporter
{
    public string Format => "docx";

    public string FileExtension => "docx";

    public Task ExportAsync(ReportDocument document, Stream output, CancellationToken cancellationToken)
    {
        using var word = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);
        var main = word.AddMainDocumentPart();
        var body = new Body();

        body.Append(CreateHeading(document.Title, sizePt: 22, bold: true));

        // Metadata block as a two-column table so it lines up neatly. The "Hinweis"
        // key is reserved for a footer paragraph rather than the metadata table.
        var metadataRows = document.Metadata
            .Where(kvp => !string.Equals(kvp.Key, "Hinweis", StringComparison.Ordinal))
            .ToList();
        if (metadataRows.Count > 0)
        {
            body.Append(BuildMetadataTable(metadataRows));
            body.Append(EmptyParagraph());
        }

        foreach (var table in document.Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            body.Append(CreateHeading(table.Title, sizePt: 16, bold: true));
            body.Append(BuildTable(table.Columns, table.Rows));
            body.Append(EmptyParagraph());
        }

        if (document.Metadata.TryGetValue("Hinweis", out var hinweis))
        {
            body.Append(CreateParagraph(hinweis, italic: true, sizePt: 9));
        }

        main.Document = new Document(body);
        main.Document.Save();
        return Task.CompletedTask;
    }

    private static Paragraph CreateHeading(string text, double sizePt, bool bold)
    {
        var runProps = new RunProperties();
        if (bold) runProps.Append(new Bold());
        runProps.Append(new FontSize { Val = ToHalfPoints(sizePt) });

        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "120", After = "60" }),
            new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph CreateParagraph(string text, bool italic = false, double sizePt = 11)
    {
        var runProps = new RunProperties();
        if (italic) runProps.Append(new Italic());
        runProps.Append(new FontSize { Val = ToHalfPoints(sizePt) });

        return new Paragraph(
            new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph EmptyParagraph() => new();

    private static Table BuildMetadataTable(IReadOnlyList<KeyValuePair<string, string>> rows)
    {
        var table = new Table();
        table.Append(BuildTableProperties());
        foreach (var (key, value) in rows)
        {
            table.Append(new TableRow(
                BuildCell(key, bold: true, shaded: true, widthPct: 30),
                BuildCell(value, widthPct: 70)));
        }

        return table;
    }

    private static Table BuildTable(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var table = new Table();
        table.Append(BuildTableProperties());

        // Header row.
        var header = new TableRow();
        foreach (var col in columns)
        {
            header.Append(BuildCell(col, bold: true, shaded: true));
        }

        table.Append(header);

        // Data rows.
        foreach (var row in rows)
        {
            var tr = new TableRow();
            foreach (var cell in row)
            {
                tr.Append(BuildCell(cell));
            }

            table.Append(tr);
        }

        return table;
    }

    private static TableProperties BuildTableProperties()
    {
        var borders = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
            new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
            new RightBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2, Color = "DDDDDD" },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 2, Color = "DDDDDD" });

        return new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, // 100%
            borders);
    }

    private static TableCell BuildCell(string text, bool bold = false, bool shaded = false, int? widthPct = null)
    {
        var runProps = new RunProperties();
        if (bold) runProps.Append(new Bold());
        runProps.Append(new FontSize { Val = ToHalfPoints(10) });

        var cell = new TableCell(new Paragraph(
            new Run(runProps, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })));

        var props = new TableCellProperties();
        if (widthPct is { } w)
        {
            props.Append(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = (w * 50).ToString() });
        }

        if (shaded)
        {
            props.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" });
        }

        cell.PrependChild(props);
        return cell;
    }

    /// <summary>Word stores font sizes in half-points (e.g. 22 = 11pt).</summary>
    private static string ToHalfPoints(double pt) => ((int)Math.Round(pt * 2)).ToString();
}

namespace DepinTracker.Infrastructure.Reports;

using System.Globalization;
using System.Text;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Renders a <see cref="ReportDocument"/> as a self-contained HTML document with
/// print-friendly A4 styling. The user opens it in any browser and uses "Save as PDF"
/// (or system print → PDF) — same end result as a native PDF, without pulling in a
/// PDF engine. Metadata is shown as a header letterhead; each table renders as a
/// titled section with zebra striping and column right-alignment for numeric columns.
/// </summary>
public sealed class HtmlReportExporter : IReportExporter
{
    public string Format => "html";

    public string FileExtension => "html";

    public async Task ExportAsync(ReportDocument document, Stream output, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);

        await writer.WriteAsync(BuildHtml(document)).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildHtml(ReportDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"de\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.Append("<title>").Append(Escape(document.Title)).AppendLine("</title>");
        sb.AppendLine(@"<style>
  :root { color-scheme: light; }
  * { box-sizing: border-box; }
  body {
    font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
    color: #1c1f24; background: #fff;
    margin: 24mm 18mm; font-size: 11pt; line-height: 1.4;
  }
  h1 { font-size: 18pt; margin: 0 0 4pt 0; }
  h2 { font-size: 13pt; margin: 18pt 0 6pt 0; border-bottom: 1px solid #ccc; padding-bottom: 2pt; }
  .meta { color: #555; font-size: 9.5pt; margin-bottom: 14pt; }
  .meta dt { float: left; clear: left; width: 9em; font-weight: 600; color: #333; }
  .meta dd { margin-left: 9.5em; }
  table { width: 100%; border-collapse: collapse; margin-top: 4pt; }
  thead th { text-align: left; border-bottom: 1.5px solid #444; padding: 4pt 6pt; font-weight: 600; }
  tbody td { padding: 3pt 6pt; border-bottom: 1px solid #eee; }
  tbody tr:nth-child(even) td { background: #fafafa; }
  /* Right-align all but the first column so numeric columns line up cleanly. */
  thead th + th, tbody td + td { text-align: right; font-variant-numeric: tabular-nums; }
  .footer { margin-top: 24pt; font-size: 9pt; color: #555; border-top: 1px solid #ccc; padding-top: 6pt; }
  @media print {
    body { margin: 18mm; }
    h2 { page-break-after: avoid; }
    table { page-break-inside: auto; }
    tr { page-break-inside: avoid; page-break-after: auto; }
    thead { display: table-header-group; }
  }
  @page { size: A4; margin: 18mm; }
</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.Append("<h1>").Append(Escape(document.Title)).AppendLine("</h1>");

        if (document.Metadata.Count > 0)
        {
            sb.AppendLine("<dl class=\"meta\">");
            foreach (var (key, value) in document.Metadata)
            {
                if (string.Equals(key, "Hinweis", StringComparison.Ordinal))
                {
                    continue; // Rendered as a footer instead.
                }

                sb.Append("  <dt>").Append(Escape(key)).AppendLine("</dt>");
                sb.Append("  <dd>").Append(Escape(value)).AppendLine("</dd>");
            }

            sb.AppendLine("</dl>");
        }

        foreach (var table in document.Tables)
        {
            sb.Append("<h2>").Append(Escape(table.Title)).AppendLine("</h2>");
            sb.AppendLine("<table>");
            sb.Append("  <thead><tr>");
            foreach (var col in table.Columns)
            {
                sb.Append("<th>").Append(Escape(col)).Append("</th>");
            }

            sb.AppendLine("</tr></thead>");

            sb.AppendLine("  <tbody>");
            foreach (var row in table.Rows)
            {
                sb.Append("    <tr>");
                foreach (var cell in row)
                {
                    sb.Append("<td>").Append(Escape(cell)).Append("</td>");
                }

                sb.AppendLine("</tr>");
            }

            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");
        }

        if (document.Metadata.TryGetValue("Hinweis", out var note))
        {
            sb.Append("<p class=\"footer\">").Append(Escape(note)).AppendLine("</p>");
        }

        sb.Append("<p class=\"footer\">Erstellt mit DePIN Tracker · ")
          .Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))
          .AppendLine("</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string Escape(string value) =>
        value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}

namespace DepinTracker.Infrastructure.Reports;

using System.Text;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Exports a report as CSV (RFC 4180 quoting). Multiple tables are written one after
/// another, separated by a blank line and a title row, so a single CSV can carry a
/// whole report.
/// </summary>
public sealed class CsvReportExporter : IReportExporter
{
    public string Format => "csv";

    public string FileExtension => "csv";

    public async Task ExportAsync(ReportDocument document, Stream output, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);

        for (var i = 0; i < document.Tables.Count; i++)
        {
            var table = document.Tables[i];
            if (i > 0)
            {
                await writer.WriteLineAsync().ConfigureAwait(false);
            }

            await writer.WriteLineAsync(Escape(table.Title)).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Join(',', table.Columns.Select(Escape))).ConfigureAwait(false);
            foreach (var row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Join(',', row.Select(Escape))).ConfigureAwait(false);
            }
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

namespace DepinTracker.Infrastructure.Reports;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Exports a report to a real .xlsx workbook via the Open XML SDK — one worksheet
/// per <see cref="ReportTable"/>. Values are written as inline strings, which keeps
/// the writer simple and the output deterministic.
/// </summary>
public sealed class ExcelReportExporter : IReportExporter
{
    public string Format => "xlsx";

    public string FileExtension => "xlsx";

    public Task ExportAsync(ReportDocument document, Stream output, CancellationToken cancellationToken)
    {
        using var spreadsheet = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
        var workbookPart = spreadsheet.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        uint sheetId = 1;
        foreach (var table in document.Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            sheetData.Append(BuildRow(table.Columns));
            foreach (var row in table.Rows)
            {
                sheetData.Append(BuildRow(row));
            }

            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId,
                Name = SanitizeSheetName(table.Title, sheetId),
            });
            sheetId++;
        }

        workbookPart.Workbook.Save();
        return Task.CompletedTask;
    }

    private static Row BuildRow(IReadOnlyList<string> values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value)),
            });
        }

        return row;
    }

    private static string SanitizeSheetName(string title, uint sheetId)
    {
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var name = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (name.Length == 0)
        {
            name = $"Sheet{sheetId}";
        }

        return name.Length > 31 ? name[..31] : name;
    }
}

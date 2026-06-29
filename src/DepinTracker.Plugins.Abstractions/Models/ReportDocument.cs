namespace DepinTracker.Plugins.Abstractions.Models;

/// <summary>A tabular section of a report: a title, column headers and string rows.</summary>
public sealed record ReportTable(
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// A format-neutral report model. Exporters render this into a concrete format
/// (CSV, XLSX, PDF, …) so adding a format is a matter of adding an exporter, not
/// reshaping the data.
/// </summary>
public sealed record ReportDocument(
    string Title,
    IReadOnlyList<ReportTable> Tables,
    IReadOnlyDictionary<string, string> Metadata);

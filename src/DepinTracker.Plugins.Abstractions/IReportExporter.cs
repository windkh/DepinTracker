namespace DepinTracker.Plugins.Abstractions;

using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Renders a <see cref="ReportDocument"/> into a concrete file format. The host
/// selects an exporter by <see cref="Format"/>; new formats (e.g. PDF) are added
/// purely by shipping a new exporter implementation.
/// </summary>
public interface IReportExporter
{
    /// <summary>Format key, e.g. <c>"csv"</c>, <c>"xlsx"</c>, <c>"pdf"</c>.</summary>
    string Format { get; }

    /// <summary>File extension (without dot) for files this exporter produces.</summary>
    string FileExtension { get; }

    Task ExportAsync(ReportDocument document, Stream output, CancellationToken cancellationToken);
}

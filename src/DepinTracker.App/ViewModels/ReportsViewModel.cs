namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Services;
using DepinTracker.Plugins.Abstractions;

/// <summary>
/// Reports page. Generates a year-scoped Finanzamt report (German labels, EUR
/// amounts). The active project comes from the app-wide scope picker in the nav
/// rail — there is no per-page project dropdown by design (single source of truth).
/// The user picks the destination via a standard Save dialog; the default filename
/// is "{year}_{project}_report.{ext}". HTML is the primary format — open in any
/// browser and use "Save as PDF" for a Finanzamt-ready PDF.
/// </summary>
public sealed class ReportsViewModel : ViewModelBase
{
    private const string HtmlFormat = "html";

    private readonly TaxReportGenerator _generator;
    private readonly IAppPaths _paths;
    private readonly IProjectScope _scope;
    private readonly IReadOnlyDictionary<string, IReportExporter> _exporters;
    private int? _selectedYear;
    private bool _includeDetail = true;
    private string _selectedFormat = HtmlFormat;
    private string _scopeLabel = string.Empty;

    public ReportsViewModel(
        TaxReportGenerator generator,
        IAppPaths paths,
        IProjectScope scope,
        IEnumerable<IReportExporter> exporters)
        : base("Reports")
    {
        _generator = generator;
        _paths = paths;
        _scope = scope;
        _exporters = exporters.ToDictionary(e => e.Format, StringComparer.OrdinalIgnoreCase);

        AvailableFormats = new ObservableCollection<string>(
            new[] { HtmlFormat }.Concat(_exporters.Keys
                .Where(f => !string.Equals(f, HtmlFormat, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)));

        GenerateCommand = new AsyncRelayCommand(GenerateAsync, () => SelectedYear is not null, ShowError);

        _scope.ActiveProjectChanged += (_, _) => RefreshScopeLabel();
        RefreshScopeLabel();
    }

    public ObservableCollection<int> AvailableYears { get; } = new();
    public ObservableCollection<string> AvailableFormats { get; }
    public AsyncRelayCommand GenerateCommand { get; }

    public int? SelectedYear { get => _selectedYear; set => SetProperty(ref _selectedYear, value); }
    public bool IncludeDetail { get => _includeDetail; set => SetProperty(ref _includeDetail, value); }
    public string SelectedFormat { get => _selectedFormat; set => SetProperty(ref _selectedFormat, value); }

    /// <summary>Display string for the currently-scoped project (read-only).</summary>
    public string ScopeLabel { get => _scopeLabel; private set => SetProperty(ref _scopeLabel, value); }

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        var years = await _generator.GetAvailableYearsAsync(cancellationToken).ConfigureAwait(true);
        AvailableYears.Clear();
        foreach (var year in years)
        {
            AvailableYears.Add(year);
        }

        SelectedYear ??= AvailableYears.FirstOrDefault();
        RefreshScopeLabel();
        StatusMessage = years.Count == 0
            ? "No imported rewards yet — import some first, then return here."
            : $"{years.Count} year(s) with reward data.";
    }

    private void RefreshScopeLabel() => ScopeLabel = $"Project: {_scope.ActiveProjectName}";

    private async Task GenerateAsync()
    {
        if (SelectedYear is null)
        {
            return;
        }

        if (!_exporters.TryGetValue(SelectedFormat, out var exporter))
        {
            StatusMessage = $"No exporter registered for format '{SelectedFormat}'.";
            return;
        }

        var projectId = _scope.ActiveProjectId;
        var projectLabel = _scope.ActiveProjectName;

        // Default filename: {year}_{project}_report.{ext}
        var defaultName = string.Format(
            CultureInfo.InvariantCulture,
            "{0}_{1}_report.{2}",
            SelectedYear.Value, Sanitize(projectLabel), exporter.FileExtension);

        Directory.CreateDirectory(_paths.Exports);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = defaultName,
            DefaultExt = exporter.FileExtension,
            InitialDirectory = _paths.Exports,
            Filter = $"{exporter.Format.ToUpperInvariant()} (*.{exporter.FileExtension})|*.{exporter.FileExtension}|All files (*.*)|*.*",
            OverwritePrompt = true,
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true)
        {
            StatusMessage = "Report generation cancelled.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Generating {SelectedYear} report for {projectLabel}…";
        try
        {
            var doc = await _generator.BuildAsync(SelectedYear.Value, projectId, IncludeDetail, CancellationToken.None)
                .ConfigureAwait(true);

            await using (var stream = File.Create(dialog.FileName))
            {
                await exporter.ExportAsync(doc, stream, CancellationToken.None).ConfigureAwait(true);
            }

            StatusMessage =
                $"Report written: {dialog.FileName}" +
                (string.Equals(SelectedFormat, HtmlFormat, StringComparison.OrdinalIgnoreCase)
                    ? " — open in a browser and use 'Save as PDF' for Finanzamt-ready output."
                    : string.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '/', '\\' }).Distinct().ToArray();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim('-');
        return string.IsNullOrEmpty(cleaned) ? "project" : cleaned;
    }

    private void ShowError(Exception ex) => StatusMessage = $"Error: {ex.Message}";
}

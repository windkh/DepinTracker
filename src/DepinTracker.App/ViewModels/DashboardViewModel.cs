namespace DepinTracker.App.ViewModels;

using System.Globalization;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Dtos;
using DepinTracker.Application.Services;

/// <summary>
/// Dashboard page: headline portfolio figures plus the rewards-over-time series for
/// the chart. Loads on activation and on demand via <see cref="RefreshCommand"/>.
/// Selecting a row in the per-year table filters both charts to that year; clearing
/// the selection (click empty space in the grid) restores the full series.
/// </summary>
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _dashboard;
    private readonly IProjectScope _scope;
    private string _portfolioValue = "—";
    private int _rewardCount;
    private int _walletCount;
    private int _projectCount;
    private int _missingPriceCount;
    private string _syncState = "—";
    private string _databaseHealth = "—";

    // Public lists are filtered views. The underlying full data lives in the
    // "_all*" fields so the user can toggle the year filter without reloading.
    private IReadOnlyList<RewardsOverTimePoint> _allPoints = Array.Empty<RewardsOverTimePoint>();
    private IReadOnlyList<TokensPerMonthPoint> _allTokenPoints = Array.Empty<TokensPerMonthPoint>();
    private IReadOnlyList<RewardsOverTimePoint> _points = Array.Empty<RewardsOverTimePoint>();
    private IReadOnlyList<YearlyRewardRow> _years = Array.Empty<YearlyRewardRow>();
    private IReadOnlyList<TokensPerMonthPoint> _tokenPoints = Array.Empty<TokensPerMonthPoint>();
    private YearlyRewardRow? _selectedYear;

    public DashboardViewModel(DashboardService dashboard, IProjectScope scope) : base("Dashboard")
    {
        _dashboard = dashboard;
        _scope = scope;
        RefreshCommand = new AsyncRelayCommand(
            () => OnActivatedAsync(CancellationToken.None),
            onError: ex => StatusMessage = $"Error: {ex.Message}");
        ClearYearFilterCommand = new RelayCommand(() => SelectedYear = null, () => SelectedYear is not null);

        // Reload whenever the user picks a different project in the nav rail.
        _scope.ActiveProjectChanged += async (_, _) => await OnActivatedAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Raised after the rewards series changes so the view can redraw the chart.</summary>
    public event EventHandler? ChartDataChanged;

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ClearYearFilterCommand { get; }

    public string PortfolioValue { get => _portfolioValue; private set => SetProperty(ref _portfolioValue, value); }
    public int RewardCount { get => _rewardCount; private set => SetProperty(ref _rewardCount, value); }
    public int WalletCount { get => _walletCount; private set => SetProperty(ref _walletCount, value); }
    public int ProjectCount { get => _projectCount; private set => SetProperty(ref _projectCount, value); }
    public int MissingPriceCount { get => _missingPriceCount; private set => SetProperty(ref _missingPriceCount, value); }
    public string SyncState { get => _syncState; private set => SetProperty(ref _syncState, value); }
    public string DatabaseHealth { get => _databaseHealth; private set => SetProperty(ref _databaseHealth, value); }

    public IReadOnlyList<RewardsOverTimePoint> Points
    {
        get => _points;
        private set => SetProperty(ref _points, value);
    }

    public IReadOnlyList<YearlyRewardRow> Years
    {
        get => _years;
        private set => SetProperty(ref _years, value);
    }

    public IReadOnlyList<TokensPerMonthPoint> TokenPoints
    {
        get => _tokenPoints;
        private set => SetProperty(ref _tokenPoints, value);
    }

    /// <summary>Currently-selected row in the per-year table; null means "show all".</summary>
    public YearlyRewardRow? SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetProperty(ref _selectedYear, value))
            {
                ApplyYearFilter();
            }
        }
    }

    /// <summary>Pre-formatted row for the per-year income table.</summary>
    public sealed record YearlyRewardRow(int Year, string FiatValue, int RewardCount);

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        StatusMessage = "Loading dashboard…";
        try
        {
            var summary = await _dashboard.GetSummaryAsync(_scope.ActiveProjectId, cancellationToken).ConfigureAwait(true);
            // Headline figure — round to 2 decimals for readability. The raw exact value
            // remains in the domain Money record for downstream calculations.
            PortfolioValue =
                $"{summary.PortfolioValue.Amount.ToString("N2", CultureInfo.InvariantCulture)} " +
                $"{summary.PortfolioValue.Currency}";
            RewardCount = summary.RewardCount;
            WalletCount = summary.WalletCount;
            ProjectCount = summary.ProjectCount;
            MissingPriceCount = summary.MissingPriceCount;
            SyncState = summary.SyncState;
            DatabaseHealth = summary.DatabaseHealth;
            _allPoints = summary.RewardsOverTime;
            _allTokenPoints = summary.TokensByMonth;
            var currency = summary.PortfolioValue.Currency;
            Years = summary.RewardsByYear
                .Select(y => new YearlyRewardRow(
                    y.Year,
                    $"{y.FiatValue.ToString("N2", CultureInfo.InvariantCulture)} {currency}",
                    y.RewardCount))
                .ToList();

            // Preserve year selection across refresh — if the previously-selected year is
            // still present, rebind to the new row; otherwise clear to "all years".
            var previouslySelectedYear = _selectedYear?.Year;
            _selectedYear = previouslySelectedYear is { } y0
                ? Years.FirstOrDefault(r => r.Year == y0)
                : null;
            ApplyYearFilter();

            StatusMessage = $"Updated. {summary.RewardCount} reward(s), {summary.MissingPriceCount} missing price(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyYearFilter()
    {
        if (_selectedYear is { } row)
        {
            Points = _allPoints.Where(p => p.Month.Year == row.Year).ToList();
            TokenPoints = _allTokenPoints.Where(p => p.Month.Year == row.Year).ToList();
        }
        else
        {
            Points = _allPoints;
            TokenPoints = _allTokenPoints;
        }

        ChartDataChanged?.Invoke(this, EventArgs.Empty);
    }
}

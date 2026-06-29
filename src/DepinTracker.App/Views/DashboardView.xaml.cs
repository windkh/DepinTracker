namespace DepinTracker.App.Views;

using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using DepinTracker.App.ViewModels;
using DepinTracker.Application.Dtos;

/// <summary>
/// Code-behind for the dashboard. ScottPlot's control is not directly bindable, so we
/// subscribe to the view model's <see cref="DashboardViewModel.ChartDataChanged"/> event
/// and redraw both bar charts (monthly fiat value and monthly token quantities) when
/// the underlying data changes.
/// </summary>
public partial class DashboardView : UserControl
{
    private DashboardViewModel? _viewModel;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Both charts represent non-negative quantities (token amounts, fiat values).
        // Lock the Y-axis bottom at 0 so panning/zooming can't reveal phantom negative
        // values that don't exist in the data.
        LockYAxisAtZero(RewardsPlot.Plot);
        LockYAxisAtZero(TokensPlot.Plot);
    }

    private static void LockYAxisAtZero(ScottPlot.Plot plot)
    {
        plot.Axes.Rules.Add(new ScottPlot.AxisRules.LockedBottom(plot.Axes.Left, 0));
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ChartDataChanged -= OnChartDataChanged;
        }

        _viewModel = e.NewValue as DashboardViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ChartDataChanged += OnChartDataChanged;
            Redraw();
        }
    }

    private void OnChartDataChanged(object? sender, System.EventArgs e) => Redraw();

    private void Redraw()
    {
        RedrawFiatByMonth();
        RedrawTokensByMonth();
    }

    private void RedrawFiatByMonth()
    {
        var plot = RewardsPlot.Plot;
        plot.Clear();

        var points = _viewModel?.Points ?? System.Array.Empty<RewardsOverTimePoint>();
        if (points.Count > 0)
        {
            var values = points.Select(p => (double)p.FiatValue).ToArray();
            plot.Add.Bars(values);

            var ticks = new ScottPlot.TickGenerators.NumericManual();
            for (var i = 0; i < points.Count; i++)
            {
                ticks.AddMajor(i, points[i].Month.ToString("yyyy-MM"));
            }

            plot.Axes.Bottom.TickGenerator = ticks;
            plot.Axes.Margins(bottom: 0);
        }

        RewardsPlot.Refresh();
    }

    private void RedrawTokensByMonth()
    {
        var plot = TokensPlot.Plot;
        plot.Clear();
        plot.Legend.IsVisible = false;

        var points = _viewModel?.TokenPoints ?? System.Array.Empty<TokensPerMonthPoint>();
        if (points.Count == 0)
        {
            TokensPlot.Refresh();
            return;
        }

        // One bar series per token symbol, with bars offset within each month so that
        // multiple tokens in the same month don't overlap. Native quantities — mixing
        // GEOD + ETH on the same axis would be meaningless, so each token uses its own
        // legend entry and the user reads each series independently.
        var months = points.Select(p => p.Month).Distinct().OrderBy(m => m).ToList();
        var monthIndex = months
            .Select((m, i) => (m, i))
            .ToDictionary(x => x.m, x => x.i);

        var tokens = points
            .Select(p => p.TokenSymbol)
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        var palette = new ScottPlot.Palettes.Category10();
        const double groupWidth = 0.8;
        var barWidth = groupWidth / System.Math.Max(1, tokens.Count);

        for (var ti = 0; ti < tokens.Count; ti++)
        {
            var token = tokens[ti];
            var bars = new List<ScottPlot.Bar>();
            foreach (var p in points.Where(p => string.Equals(p.TokenSymbol, token, System.StringComparison.OrdinalIgnoreCase)))
            {
                var center = monthIndex[p.Month] - (groupWidth / 2.0) + barWidth * (ti + 0.5);
                bars.Add(new ScottPlot.Bar
                {
                    Position = center,
                    Value = (double)p.Amount,
                    Size = barWidth * 0.9,
                    FillColor = palette.GetColor(ti),
                });
            }

            var series = plot.Add.Bars(bars);
            series.LegendText = token;
        }

        var ticks = new ScottPlot.TickGenerators.NumericManual();
        for (var i = 0; i < months.Count; i++)
        {
            ticks.AddMajor(i, months[i].ToString("yyyy-MM"));
        }

        plot.Axes.Bottom.TickGenerator = ticks;
        plot.Axes.Margins(bottom: 0);
        plot.Legend.IsVisible = tokens.Count > 1;

        TokensPlot.Refresh();
    }
}

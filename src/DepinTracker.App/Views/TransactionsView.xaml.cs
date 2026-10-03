namespace DepinTracker.App.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DepinTracker.App.ViewModels;

/// <summary>
/// Code-behind for the transactions page. Only view glue lives here: opening the
/// "Import / Add" drop-down, forwarding the grid's multi-selection (DataGrid.SelectedItems
/// isn't bindable) and scrolling newly imported rows into view.
/// </summary>
public partial class TransactionsView : UserControl
{
    private TransactionsViewModel? _viewModel;

    public TransactionsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.NewRowsAdded -= OnNewRowsAdded;
        }

        _viewModel = e.NewValue as TransactionsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.NewRowsAdded += OnNewRowsAdded;
        }
    }

    private void OnImportMenuClick(object sender, RoutedEventArgs e)
    {
        var menu = ImportMenuButton.ContextMenu;
        menu.PlacementTarget = ImportMenuButton;

        // Pin the menu under the button's left edge. PlacementMode.Bottom right-aligns it
        // when Windows is set to right-handed menus (SystemParameters.MenuDropAlignment).
        menu.Placement = PlacementMode.Custom;
        menu.CustomPopupPlacementCallback = (_, target, _) =>
            new[] { new CustomPopupPlacement(new Point(0, target.Height), PopupPrimaryAxis.Horizontal) };
        menu.IsOpen = true;
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _viewModel?.SetSelection(TransactionsGrid.SelectedItems.OfType<TransactionRow>());

    private void OnNewRowsAdded(object? sender, TransactionRow row) => TransactionsGrid.ScrollIntoView(row);
}

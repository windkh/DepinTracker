namespace DepinTracker.App;

using System.Windows;

/// <summary>
/// The shell window. Its DataContext (a MainViewModel) is assigned by the
/// composition root in <see cref="App"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}

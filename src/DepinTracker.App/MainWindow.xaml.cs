namespace DepinTracker.App;

using System.Reflection;
using System.Windows;

/// <summary>
/// The shell window. Its DataContext (a MainViewModel) is assigned by the
/// composition root in <see cref="App"/>. The window title is suffixed with the
/// build version stamped in by the SetVersionFromGit MSBuild target so the user
/// can verify at a glance which build they are running.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = $"DePIN Tracker {ReadVersion()}";
    }

    private static string ReadVersion()
    {
        var asm = typeof(MainWindow).Assembly;
        // InformationalVersion carries the git short SHA suffix (e.g. "0.1.42+abc1234")
        // when the build ran inside a git checkout; falls back to the assembly version
        // otherwise.
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            return info;
        }

        var v = asm.GetName().Version;
        return v is null ? "?" : v.ToString(3);
    }
}

namespace DepinTracker.App.Services;

using System.Windows;

/// <summary><see cref="IDialogService"/> backed by the standard WPF message box.</summary>
public sealed class MessageBoxDialogService : IDialogService
{
    public bool Confirm(string title, string message)
    {
        const MessageBoxButton buttons = MessageBoxButton.YesNo;
        const MessageBoxImage icon = MessageBoxImage.Warning;
        var owner = System.Windows.Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, title, buttons, icon, MessageBoxResult.No)
            : MessageBox.Show(owner, message, title, buttons, icon, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }
}

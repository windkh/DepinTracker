namespace DepinTracker.App.Services;

/// <summary>
/// Modal prompts raised by view models. Abstracted so view models never touch
/// <c>MessageBox</c> directly and stay usable without a UI thread.
/// </summary>
public interface IDialogService
{
    /// <summary>Asks a yes/no question; returns <c>true</c> when the user confirms.</summary>
    bool Confirm(string title, string message);
}

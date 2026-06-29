namespace DepinTracker.App.ViewModels;

using DepinTracker.App.Mvvm;

/// <summary>
/// Base for page view models. Carries a navigation <see cref="Title"/>, a shared
/// busy flag and status line, and an activation hook the shell calls when the page
/// becomes visible so each page loads its data lazily.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    protected ViewModelBase(string title) => Title = title;

    public string Title { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Called by the shell when this page is selected. Override to load data.</summary>
    public virtual Task OnActivatedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

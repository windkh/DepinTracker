namespace DepinTracker.App.Mvvm;

using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// Minimal hand-rolled MVVM base implementing <see cref="INotifyPropertyChanged"/>.
/// Hand-rolled (rather than depending on a MVVM toolkit) to keep the dependency
/// surface exactly as the specification lists.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

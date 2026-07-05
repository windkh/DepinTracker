namespace DepinTracker.App.Mvvm;

using System.Globalization;
using System.Windows.Data;

/// <summary>
/// Returns <c>!input</c> for bool bindings. Used by the welcome page to grey out
/// the "create project" form once the user has already created their first project.
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}

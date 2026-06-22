using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpacePilot.Converters;

public sealed class SectionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var selected = value?.ToString();
        var expected = parameter?.ToString();
        return string.Equals(selected, expected, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

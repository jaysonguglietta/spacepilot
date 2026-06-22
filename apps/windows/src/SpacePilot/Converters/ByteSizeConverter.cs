using System.Globalization;
using System.Windows.Data;
using SpacePilot.Utilities;

namespace SpacePilot.Converters;

public sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            long bytes => Formatters.FormatBytes(bytes),
            int bytes => Formatters.FormatBytes(bytes),
            null => Formatters.FormatBytes(0),
            _ => value
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

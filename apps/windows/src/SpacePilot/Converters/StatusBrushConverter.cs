using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SpacePilot.Models;

namespace SpacePilot.Converters;

public sealed class StatusBrushConverter : IValueConverter
{
    private static readonly Brush GoodBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50));
    private static readonly Brush AttentionBrush = new SolidColorBrush(Color.FromRgb(239, 108, 0));
    private static readonly Brush UnknownBrush = new SolidColorBrush(Color.FromRgb(96, 125, 139));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            SettingsCheckStatus.Good => GoodBrush,
            SettingsCheckStatus.Attention => AttentionBrush,
            SettingsCheckStatus.Unknown => UnknownBrush,
            _ => UnknownBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

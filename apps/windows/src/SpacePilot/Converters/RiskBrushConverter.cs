using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SpacePilot.Models;

namespace SpacePilot.Converters;

public sealed class RiskBrushConverter : IValueConverter
{
    private static readonly Brush LowBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50));
    private static readonly Brush MediumBrush = new SolidColorBrush(Color.FromRgb(239, 108, 0));
    private static readonly Brush HighBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            RiskLevel.Low => LowBrush,
            RiskLevel.Medium => MediumBrush,
            RiskLevel.High => HighBrush,
            _ => MediumBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

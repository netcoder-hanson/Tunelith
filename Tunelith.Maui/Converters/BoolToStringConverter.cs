using System.Globalization;

namespace Tunelith.Maui.Converters;

public class BoolToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string values)
        {
            var parts = values.Split('|');
            if (parts.Length == 2)
                return b ? parts[0] : parts[1];
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

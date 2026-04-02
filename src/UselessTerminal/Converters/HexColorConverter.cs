using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UselessTerminal.Converters;

public sealed class HexColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return Colors.Gray;
        try
        {
            var o = System.Windows.Media.ColorConverter.ConvertFromString(s.Trim());
            return o is Color c ? c : Colors.Gray;
        }
        catch
        {
            return Colors.Gray;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

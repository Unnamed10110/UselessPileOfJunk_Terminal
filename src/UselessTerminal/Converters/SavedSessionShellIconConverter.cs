using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UselessTerminal.Models;
using UselessTerminal.Services;

namespace UselessTerminal.Converters;

public sealed class SavedSessionShellIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SavedSession s)
            return ShellIconLoader.FallbackIcon;
        return ShellIconLoader.GetIconForPath(s.ShellPath);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

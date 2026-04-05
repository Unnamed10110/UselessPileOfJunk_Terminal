using System.Globalization;
using System.Windows.Data;

namespace UselessTerminal.Converters;

/// <summary>
/// Caps header content width to the TreeView row: ActualWidth minus the expander column when <see cref="System.Windows.Controls.TreeViewItem.HasItems"/>.
/// Prevents session cards from measuring wider than the visible panel (right-side clip).
/// </summary>
public sealed class TreeViewItemHeaderMaxWidthConverter : IMultiValueConverter
{
    private const double ExpanderColumnWidth = 18;
    /// <summary>Slack for TreeView padding, card border/padding, and layout rounding.</summary>
    private const double SafetyMargin = 6;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return double.PositiveInfinity;

        double aw = 0;
        if (values[0] is double d)
            aw = d;
        else if (values[0] is float f)
            aw = f;
        else
            return double.PositiveInfinity;

        if (aw <= 0 || double.IsNaN(aw) || double.IsInfinity(aw))
            return double.PositiveInfinity;

        bool hasItems = values[1] is bool h && h;
        double w = aw - (hasItems ? ExpanderColumnWidth : 0) - SafetyMargin;
        return Math.Max(32, w);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

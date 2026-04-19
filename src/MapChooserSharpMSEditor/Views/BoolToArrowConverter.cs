using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MapChooserSharpMSEditor.Views;

/// <summary>
/// true → "&#xE76C;" (chevron-right-to-left, "collapse"),
/// false → "&#xE76B;" (chevron-left-to-right, "expand").
/// Used by the resolved-panel toggle strip.
/// </summary>
public sealed class BoolToArrowConverter : IValueConverter
{
    public static readonly BoolToArrowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var open = value is bool b && b;
        return open ? "\uE76C" : "\uE76B";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

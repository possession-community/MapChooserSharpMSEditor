using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MapChooserSharpMSEditor.Views;

/// <summary>
/// true → "▲" (expanded, click to collapse),
/// false → "▼" (collapsed, click to expand).
/// Plain Unicode (not Segoe Fluent) so it renders regardless of font availability.
/// </summary>
public sealed class BoolToChevronConverter : IValueConverter
{
    public static readonly BoolToChevronConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var open = value is bool b && b;
        return open ? "\u25B2" : "\u25BC";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

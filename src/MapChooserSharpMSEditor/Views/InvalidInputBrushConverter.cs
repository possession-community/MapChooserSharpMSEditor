using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Views;

/// <summary>
/// Paints a TextBox border red when the bound IsValid flag is false. Returns
/// <see cref="BindingOperations.DoNothing"/> when valid so the theme's default border is
/// preserved (no hard-coded grey override).
/// </summary>
public sealed class InvalidInputBrushConverter : IValueConverter
{
    public static readonly InvalidInputBrushConverter Instance = new();
    private static readonly IBrush _invalid = new SolidColorBrush(Color.Parse("#e05a5a"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool ok && !ok ? _invalid : BindingOperations.DoNothing;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Tooltip helper: show the "invalid time range" message when validity is false,
/// otherwise no tooltip (so valid input doesn't pop a noisy hover).</summary>
public sealed class InvalidTimeRangeTipConverter : IValueConverter
{
    public static readonly InvalidTimeRangeTipConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool ok && !ok ? Localization.Get("Tip.InvalidTimeRange") : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

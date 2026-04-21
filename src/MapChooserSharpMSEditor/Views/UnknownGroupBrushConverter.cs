using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Views;

/// <summary>
/// Paints group-reference labels red when the bound <c>IsKnown</c> flag is false. The
/// warning icon is separate; this just ensures the name itself reads as "broken" even
/// when the user isn't hovering to see the tooltip.
/// </summary>
public sealed class UnknownGroupBrushConverter : IValueConverter
{
    public static readonly UnknownGroupBrushConverter Instance = new();
    private static readonly IBrush _known = Brushes.White;
    private static readonly IBrush _unknown = new SolidColorBrush(Color.Parse("#f08a7a"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isKnown && !isKnown ? _unknown : _known;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Return the "unknown group" tooltip string when IsKnown is false, else null
/// (no tooltip) so regular group-ref rows don't pop a noisy hover hint.</summary>
public sealed class UnknownGroupTipConverter : IValueConverter
{
    public static readonly UnknownGroupTipConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isKnown && !isKnown ? Localization.Get("Tip.UnknownGroup") : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

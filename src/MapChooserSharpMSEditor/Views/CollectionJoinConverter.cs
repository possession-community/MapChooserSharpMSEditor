using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace MapChooserSharpMSEditor.Views;

/// <summary>
/// Joins an <see cref="IEnumerable"/> with commas for display.
/// Avoids the default ObservableCollection.ToString() which prints the type name.
/// Returns "(none)" for empty/null so text bindings don't collapse to nothing mid-sentence.
/// </summary>
public sealed class CollectionJoinConverter : IValueConverter
{
    public static readonly CollectionJoinConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable e || value is string)
            return value?.ToString() ?? "";

        var items = e.Cast<object?>().Select(x => x?.ToString() ?? "").ToList();
        return items.Count == 0 ? "(none)" : string.Join(", ", items);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

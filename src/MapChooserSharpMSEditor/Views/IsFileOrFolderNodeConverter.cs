using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MapChooserSharpMSEditor.ViewModels.TreeNodes;

namespace MapChooserSharpMSEditor.Views;

/// <summary>
/// true when the tree node is a FileNode or FolderNode (the two kinds that correspond to
/// something on disk and therefore have a meaningful "Remove from view" action). Used by
/// the Explorer context menu to hide the item for structural-only nodes (Default /
/// Category / Map / Group / Override) where removal wouldn't make sense.
/// </summary>
public sealed class IsFileOrFolderNodeConverter : IValueConverter
{
    public static readonly IsFileOrFolderNodeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FileNode or FolderNode;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

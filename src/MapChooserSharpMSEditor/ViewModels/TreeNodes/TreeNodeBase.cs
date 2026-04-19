using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.ViewModels.TreeNodes;

/// <summary>
/// A single row inside the left-hand tree. Selection is driven by the
/// <see cref="MainWindowViewModel"/> — selecting a node swaps the editor.
/// </summary>
public abstract partial class TreeNodeBase : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _icon = string.Empty;
    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<TreeNodeBase> Children { get; } = new();
}

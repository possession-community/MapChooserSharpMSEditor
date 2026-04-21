using Avalonia.Controls;
using Avalonia.Threading;

namespace MapChooserSharpMSEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Scroll the newly-selected tree node into view. Without this, navigating from the search
    /// window to a node deep in a collapsed hierarchy leaves the tree scrolled away from the
    /// selection, so the user can't see where they landed. Posted to the dispatcher so the
    /// container has time to realize after IsExpanded propagates through the style binding.
    /// </summary>
    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TreeView tv || tv.SelectedItem is null) return;
        var selected = tv.SelectedItem;
        Dispatcher.UIThread.Post(() => tv.ScrollIntoView(selected), DispatcherPriority.Background);
    }
}

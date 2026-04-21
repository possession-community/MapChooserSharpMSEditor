using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Views;

public partial class DebugConsolePanel : UserControl
{
    public DebugConsolePanel()
    {
        InitializeComponent();
        // Auto-scroll to the newest entry so debugging sessions don't require manual
        // scrolling after each action. Posted to the dispatcher so the ScrollViewer's
        // Extent has a chance to update before we measure.
        Log.Entries.CollectionChanged += OnLogEntriesChanged;
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        Dispatcher.UIThread.Post(() =>
        {
            var scroll = this.FindControl<ScrollViewer>("Scroll");
            scroll?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e) => Log.Clear();
}

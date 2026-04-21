using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MapChooserSharpMSEditor.ViewModels;

namespace MapChooserSharpMSEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Mouse XButton1 (back) / XButton2 (forward) as browser-style nav over the view
        // history. AddHandler with Tunnel routing catches the press even when a child
        // control (e.g. a TextBox) would otherwise swallow it.
        AddHandler(PointerPressedEvent, OnGlobalPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsXButton1Pressed)
        {
            if (vm.NavigateBackCommand.CanExecute(null))
            {
                vm.NavigateBackCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (props.IsXButton2Pressed)
        {
            if (vm.NavigateForwardCommand.CanExecute(null))
            {
                vm.NavigateForwardCommand.Execute(null);
                e.Handled = true;
            }
        }
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

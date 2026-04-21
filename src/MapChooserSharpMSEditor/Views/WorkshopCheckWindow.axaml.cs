using Avalonia.Controls;
using Avalonia.Interactivity;
using MapChooserSharpMSEditor.ViewModels;

namespace MapChooserSharpMSEditor.Views;

public partial class WorkshopCheckWindow : Window
{
    public WorkshopCheckWindow() => InitializeComponent();

    /// <summary>
    /// Apply-and-close: flips IsDisabled=true on every checked row and closes the window.
    /// Counts are surfaced via the status bar of the owning window (caller reads the
    /// result of <see cref="WorkshopCheckViewModel.ApplySelection"/>).
    /// </summary>
    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkshopCheckViewModel vm)
        {
            var count = vm.ApplySelection();
            Tag = count; // caller reads Tag to know how many rows were touched
        }
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}

// LEGACY — remove when MCS migration completes
using Avalonia.Controls;
using Avalonia.Interactivity;
using MapChooserSharpMSEditor.ViewModels.Legacy;

namespace MapChooserSharpMSEditor.Views.Legacy;

public partial class LegacyBranchDiffWindow : Window
{
    public LegacyBranchDiffWindow()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnEntryClick);
    }

    /// <summary>
    /// The entry list rows are Buttons whose Tag carries the LegacyDiffEntry; rather than
    /// wiring a dedicated command per click (which requires selection-changed events that
    /// ItemsControl doesn't surface), we intercept the routed Click and set the VM
    /// selection directly from Tag.
    /// </summary>
    private void OnEntryClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { Tag: LegacyDiffEntry entry }
            && DataContext is LegacyBranchDiffViewModel vm)
        {
            vm.SelectedEntry = entry;
        }
    }
}

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.ViewModels.Editors;

namespace MapChooserSharpMSEditor.Views;

public partial class OverrideEditorView : UserControl
{
    public OverrideEditorView() => InitializeComponent();

    private void OnToggleTargetDay(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is DayOfWeek day && DataContext is OverrideEditorViewModel vm)
        {
            if (vm.Override.TargetDays.Contains(day))
                vm.Override.TargetDays.Remove(day);
            else
                vm.Override.TargetDays.Add(day);
        }
    }

    private void OnAddTargetTimeRange(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OverrideEditorViewModel vm) return;
        var box = this.FindControl<TextBox>("NewRangeBox");
        if (box is null) return;
        if (TimeRangeSpec.TryParse(box.Text, out var r) && r is not null)
        {
            vm.Override.TargetTimeRanges.Add(r);
            box.Text = string.Empty;
        }
    }

    private void OnRemoveTargetTimeRange(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is TimeRangeSpec range && DataContext is OverrideEditorViewModel vm)
            vm.Override.TargetTimeRanges.Remove(range);
    }
}

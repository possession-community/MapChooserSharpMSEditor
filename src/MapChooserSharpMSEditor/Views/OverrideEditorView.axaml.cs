using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.ViewModels.Editors;

namespace MapChooserSharpMSEditor.Views;

public partial class OverrideEditorView : UserControl
{
    private static readonly IBrush _invalidBrush = new SolidColorBrush(Color.Parse("#e05a5a"));

    public OverrideEditorView() => InitializeComponent();

    /// <summary>
    /// Live validation for the TargetTimeRanges input: tint the border red when the text
    /// doesn't parse as <c>HH:mm-HH:mm</c>. Empty is treated as valid so an untouched
    /// field doesn't look broken before the user types anything.
    /// </summary>
    private void OnNewRangeTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var ok = string.IsNullOrWhiteSpace(box.Text) || TimeRangeSpec.TryParse(box.Text, out _);
        box.BorderBrush = ok ? null : _invalidBrush;
        ToolTip.SetTip(box, ok ? null : Localization.Get("Tip.InvalidTimeRange"));
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

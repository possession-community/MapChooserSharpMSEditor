// LEGACY — remove when MCS migration completes
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

namespace MapChooserSharpMSEditor.Views.Legacy;

public partial class LegacyPropertySetView : UserControl
{
    // Distinct format names from Current's PropertySetView so a stray drop event from
    // a Current row doesn't mutate Legacy state (and vice versa).
    private const string GroupRefFormat = "mcs.legacy.group-ref";
    private const string TimeRangeFormat = "mcs.legacy.time-range";

    public LegacyPropertySetView() => InitializeComponent();

    private async void OnGroupRefPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not LegacyGroupRefViewModel gr) return;
        if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;

        var data = new DataObject();
        data.Set(GroupRefFormat, gr.Name);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnGroupRefDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(GroupRefFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnGroupRefDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(GroupRefFormat)) return;
        if (e.Data.Get(GroupRefFormat) is not string sourceName) return;
        if (sender is not Control c || c.DataContext is not LegacyGroupRefViewModel target) return;
        if (DataContext is not LegacyPropertySetViewModel vm) return;

        var coll = vm.Model.GroupSettings;
        var from = coll.IndexOf(sourceName);
        var to = coll.IndexOf(target.Name);
        if (from < 0 || to < 0 || from == to) { e.Handled = true; return; }

        using var _ = vm.Project?.Undo.BeginBatch("Move group reference");
        coll.RemoveAt(from);
        var adjusted = from < to ? to - 1 : to;
        coll.Insert(Math.Clamp(adjusted, 0, coll.Count), sourceName);
        e.Handled = true;
    }

    private async void OnTimeRangePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not TimeRangeSpec spec) return;
        if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;

        var data = new DataObject();
        data.Set(TimeRangeFormat, spec.ToString());
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnTimeRangeDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(TimeRangeFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTimeRangeDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(TimeRangeFormat)) return;
        if (e.Data.Get(TimeRangeFormat) is not string sourceStr) return;
        if (sender is not Control c || c.DataContext is not TimeRangeSpec target) return;
        if (DataContext is not LegacyPropertySetViewModel vm) return;
        if (!TimeRangeSpec.TryParse(sourceStr, out var source) || source is null) return;

        var coll = vm.Model.AllowedTimeRanges;
        var from = IndexOfRange(coll, source);
        var to = coll.IndexOf(target);
        if (from < 0 || to < 0 || from == to) { e.Handled = true; return; }

        using var _ = vm.Project?.Undo.BeginBatch("Move time range");
        var item = coll[from];
        coll.RemoveAt(from);
        var adjusted = from < to ? to - 1 : to;
        coll.Insert(Math.Clamp(adjusted, 0, coll.Count), item);
        e.Handled = true;
    }

    private static int IndexOfRange(ObservableCollection<TimeRangeSpec> coll, TimeRangeSpec target)
    {
        for (var i = 0; i < coll.Count; i++)
            if (coll[i].Equals(target))
                return i;
        return -1;
    }
}

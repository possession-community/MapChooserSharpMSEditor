using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.ViewModels.Editors;

namespace MapChooserSharpMSEditor.Views;

public partial class PropertySetView : UserControl
{
    // Transported payload formats — strings so Avalonia's DataObject serializes cleanly.
    private const string GroupRefFormat = "mcs.group-ref";
    private const string TimeRangeFormat = "mcs.time-range";

    public PropertySetView() => InitializeComponent();

    /// <summary>
    /// Starts a drag for a GroupReferences row. Ignores presses whose source is a Button so
    /// the existing ▲/▼/× controls still fire cleanly; buttons get their click before any
    /// drag has a chance to kick in.
    /// </summary>
    private async void OnGroupRefPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInsideButton(e.Source as Visual, sender as Visual)) return;
        if (sender is not Control c || c.DataContext is not GroupRefViewModel gr) return;
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
        if (sender is not Control c || c.DataContext is not GroupRefViewModel target) return;
        if (DataContext is not PropertySetViewModel vm) return;

        var coll = vm.Model.GroupSettings;
        var from = coll.IndexOf(sourceName);
        var to = coll.IndexOf(target.Name);
        if (from < 0 || to < 0 || from == to) { e.Handled = true; return; }

        using var _ = vm.Project?.Undo.BeginBatch("Move group reference");
        coll.RemoveAt(from);
        // When dragging down, pulling the item out of its old slot shifts later indices left by 1.
        var adjusted = from < to ? to - 1 : to;
        coll.Insert(Math.Clamp(adjusted, 0, coll.Count), sourceName);
        e.Handled = true;
    }

    private async void OnTimeRangePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInsideButton(e.Source as Visual, sender as Visual)) return;
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
        if (DataContext is not PropertySetViewModel vm) return;
        if (!TimeRangeSpec.TryParse(sourceStr, out var source) || source is null) return;

        var coll = vm.Model.AllowedTimeRanges;
        // Match by string form because TimeRangeSpec is a record and instance identity is lost
        // when the payload crosses the clipboard boundary.
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

    /// <summary>
    /// Walks from the click source up to the row root; returns true if a Button sits in
    /// between, meaning we should treat this click as a Button press, not a drag initiation.
    /// </summary>
    private static bool IsInsideButton(Visual? source, Visual? stopAt)
    {
        var cur = source;
        while (cur is not null && cur != stopAt)
        {
            if (cur is Button) return true;
            cur = cur.GetVisualParent();
        }
        return false;
    }
}

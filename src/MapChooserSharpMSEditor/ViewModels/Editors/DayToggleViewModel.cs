using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

/// <summary>
/// One day-of-week chip backed by an underlying <see cref="ObservableCollection{DayOfWeek}"/>
/// (the model's DaysAllowed / TargetDays list). <see cref="IsSelected"/> is a two-way
/// projection: toggling it adds or removes the day from the target collection, and
/// external mutations of that collection flow back into IsSelected so the chip stays in
/// sync with undo / reset / external edits.
/// </summary>
public sealed partial class DayToggleViewModel : ObservableObject
{
    public DayOfWeek Day { get; }
    private readonly ObservableCollection<DayOfWeek> _target;

    [ObservableProperty] private bool _isSelected;

    public DayToggleViewModel(DayOfWeek day, ObservableCollection<DayOfWeek> target)
    {
        Day = day;
        _target = target;
        _isSelected = target.Contains(day);
        target.CollectionChanged += OnTargetChanged;
    }

    private void OnTargetChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Suppress re-entry when the VM itself is mid-update (we only write to _target
        // inside OnIsSelectedChanged, and the collection change fires during that write).
        var inList = _target.Contains(Day);
        if (IsSelected != inList) IsSelected = inList;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        var inList = _target.Contains(Day);
        if (value && !inList) _target.Add(Day);
        else if (!value && inList) _target.Remove(Day);
    }
}

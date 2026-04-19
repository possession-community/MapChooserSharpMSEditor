using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

/// <summary>
/// A single entry in <see cref="PropertySet.GroupSettings"/> decorated with an IsKnown flag
/// that reflects <see cref="ProjectContext.AllGroupNames"/>.
/// </summary>
public partial class GroupRefViewModel : ObservableObject
{
    public string Name { get; }
    [ObservableProperty] private bool _isKnown;

    public GroupRefViewModel(string name, bool isKnown)
    {
        Name = name;
        _isKnown = isKnown;
    }
}

/// <summary>
/// Wraps a <see cref="PropertySet"/> for UI binding.
///
/// UX contract:
///   * Every property exposes a paired <c>HasX</c> flag that controls whether it is emitted to TOML.
///   * Editing a value — typing in a box, toggling a bool, mutating a list — automatically sets
///     <c>HasX = true</c>. Users do not need to tick a checkbox first.
///   * The <see cref="ClearOverrideCommand"/> flips <c>HasX = false</c> without touching the
///     underlying value so the display can fall back to "(using default)".
///   * The <see cref="SetOverrideCommand"/> is the reverse: explicit opt-in to override when the
///     user wants to accept the current default value as their own.
/// </summary>
public partial class PropertySetViewModel : ViewModelBase
{
    public PropertySet Model { get; }

    /// <summary>Project-wide context for cross-file concerns like group-name autocomplete.</summary>
    public ProjectContext? Project { get; }

    public ObservableCollection<GroupRefViewModel> GroupReferences { get; } = new();

    public PropertySetViewModel(PropertySet model, ProjectContext? project = null)
    {
        Model = model;
        Project = project;

        RebuildGroupReferences();
        // The Has* auto-set now lives on PropertySet itself so that, from the undo system's
        // perspective, the flag flip happens inside the value-change's stack frame and gets
        // batched into the same undo entry. The VM just refreshes its derived UI list.
        Model.GroupSettings.CollectionChanged += (_, _) => RebuildGroupReferences();

        if (Project is not null)
            Project.AllGroupNames.CollectionChanged += (_, _) => RefreshGroupRefValidity();
    }

    private void RebuildGroupReferences()
    {
        GroupReferences.Clear();
        foreach (var name in Model.GroupSettings)
            GroupReferences.Add(new GroupRefViewModel(name, Project?.IsKnownGroup(name) ?? true));
    }

    private void RefreshGroupRefValidity()
    {
        foreach (var gr in GroupReferences)
            gr.IsKnown = Project?.IsKnownGroup(gr.Name) ?? true;
    }

    public static DayOfWeek[] AllDays { get; } =
    {
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    };

    [ObservableProperty] private string _newTimeRange = string.Empty;
    [ObservableProperty] private string _newGroupName = string.Empty;
    [ObservableProperty] private string _newExtraSectionName = string.Empty;

    [RelayCommand]
    private void SetOverride(string? propertyName) => SetHasFlag(propertyName, true);

    [RelayCommand]
    private void ClearOverride(string? propertyName) => SetHasFlag(propertyName, false);

    private void SetHasFlag(string? propertyName, bool value)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        // Wrap the whole operation in an undo batch so Ctrl+Z restores collection contents
        // and the HasX flag in one step. We deliberately use iterative RemoveAt instead of
        // Clear(): Clear emits a Reset event that carries no item data, which our hooks
        // cannot undo. RemoveAt emits one Remove per item — each recordable.
        using var _ = Project?.Undo.BeginBatch("Reset " + propertyName);

        if (!value)
        {
            switch (propertyName)
            {
                case "GroupSettings":
                    for (var i = Model.GroupSettings.Count - 1; i >= 0; i--) Model.GroupSettings.RemoveAt(i);
                    break;
                case "DaysAllowed":
                    for (var i = Model.DaysAllowed.Count - 1; i >= 0; i--) Model.DaysAllowed.RemoveAt(i);
                    break;
                case "AllowedTimeRanges":
                    for (var i = Model.AllowedTimeRanges.Count - 1; i >= 0; i--) Model.AllowedTimeRanges.RemoveAt(i);
                    break;
            }
        }

        var hasProp = typeof(PropertySet).GetProperty("Has" + propertyName, BindingFlags.Public | BindingFlags.Instance);
        hasProp?.SetValue(Model, value);
    }

    // Collection-mutating commands are wrapped in undo batches so the collection change
    // and its paired HasX auto-set (from PropertySet.cs) collapse into a single Ctrl+Z step.
    [RelayCommand]
    private void AddTimeRange()
    {
        if (TimeRangeSpec.TryParse(NewTimeRange, out var r) && r is not null)
        {
            using var _ = Project?.Undo.BeginBatch("Add time range");
            Model.AllowedTimeRanges.Add(r);
            NewTimeRange = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveTimeRange(TimeRangeSpec range)
    {
        using var _ = Project?.Undo.BeginBatch("Remove time range");
        Model.AllowedTimeRanges.Remove(range);
    }

    [RelayCommand]
    private void ToggleDay(DayOfWeek day)
    {
        using var _ = Project?.Undo.BeginBatch("Toggle day");
        if (Model.DaysAllowed.Contains(day)) Model.DaysAllowed.Remove(day);
        else Model.DaysAllowed.Add(day);
    }

    [RelayCommand]
    private void AddGroupReference()
    {
        var name = NewGroupName?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        using var _ = Project?.Undo.BeginBatch("Add group reference");
        if (!Model.GroupSettings.Contains(name))
            Model.GroupSettings.Add(name);
        NewGroupName = string.Empty;
    }

    [RelayCommand]
    private void RemoveGroupReference(GroupRefViewModel? reference)
    {
        if (reference is null) return;
        using var _ = Project?.Undo.BeginBatch("Remove group reference");
        Model.GroupSettings.Remove(reference.Name);
    }

    [RelayCommand]
    private void AddExtraSection()
    {
        var name = NewExtraSectionName?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        using var _ = Project?.Undo.BeginBatch("Add extra section");
        Model.Extras.Add(new ExtraSection { Name = name });
        NewExtraSectionName = string.Empty;
    }

    [RelayCommand]
    private void RemoveExtraSection(ExtraSection section)
    {
        using var _ = Project?.Undo.BeginBatch("Remove extra section");
        Model.Extras.Remove(section);
    }

    [RelayCommand]
    private void AddExtraKey(ExtraSection section)
    {
        using var _ = Project?.Undo.BeginBatch("Add extra key");
        section.Entries.Add(new ExtraKeyValue { Key = "new_key", Value = string.Empty });
    }
}

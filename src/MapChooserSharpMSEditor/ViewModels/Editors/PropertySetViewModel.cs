using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

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

    /// <summary>
    /// Scope of this property set. Drives per-row visibility: hides group-only fields in
    /// map contexts and vice versa so the UI matches the server's accepted schema.
    /// </summary>
    public PropertyScope Scope { get; }

    /// <summary>
    /// Provider for the parent inheritance chain (immediate parent first, root default last).
    /// Called at Reset time so the latest state is used — group/map references might have
    /// changed since this VM was constructed.
    /// </summary>
    private readonly Func<IReadOnlyList<PropertySet>>? _inheritanceChain;

    public ObservableCollection<GroupRefViewModel> GroupReferences { get; } = new();

    // Row visibility bindings. Default is permissive (useful as a project-wide fallback).
    public bool ShowMapNameAlias => Scope is PropertyScope.Default or PropertyScope.Map;
    public bool ShowMapDescription => Scope is PropertyScope.Default or PropertyScope.Map;
    public bool ShowWorkshopId => Scope is PropertyScope.Default or PropertyScope.Map;
    public bool ShowGroupSettings => Scope is PropertyScope.Default or PropertyScope.Map;
    public bool ShowSearchTags => true;
    public bool ShowCooldownOverride => Scope is PropertyScope.Default or PropertyScope.Group;
    public bool ShowShortGroupName => Scope is PropertyScope.Default or PropertyScope.Group;
    public bool ShowNominationLimit => Scope is PropertyScope.Default or PropertyScope.Group;

    /// <summary>
    /// Localized phrase shown where a row has no override. Override editors pass a
    /// "from override target" variant so the user isn't misled into thinking the base
    /// map/group values are being ignored.
    /// </summary>
    public string InheritedStatusText { get; }

    public PropertySetViewModel(
        PropertySet model,
        PropertyScope scope = PropertyScope.Default,
        ProjectContext? project = null,
        Func<IReadOnlyList<PropertySet>>? inheritanceChain = null,
        string inheritedStatusKey = "Status.UsingDefault")
    {
        Model = model;
        Scope = scope;
        Project = project;
        _inheritanceChain = inheritanceChain;
        InheritedStatusText = Localization.Get(inheritedStatusKey);

        RebuildGroupReferences();
        // The Has* auto-set now lives on PropertySet itself so that, from the undo system's
        // perspective, the flag flip happens inside the value-change's stack frame and gets
        // batched into the same undo entry. The VM just refreshes its derived UI list.
        Model.GroupSettings.CollectionChanged += (_, _) => RebuildGroupReferences();

        if (Project is not null)
            Project.AllGroupNames.CollectionChanged += (_, _) => RefreshGroupRefValidity();

        DaysAllowedToggles = BuildDayToggles(Model.DaysAllowed);
    }

    private static IReadOnlyList<DayToggleViewModel> BuildDayToggles(ObservableCollection<DayOfWeek> target)
    {
        var list = new List<DayToggleViewModel>(AllDays.Length);
        foreach (var d in AllDays) list.Add(new DayToggleViewModel(d, target));
        return list;
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

    /// <summary>
    /// One chip per day bound to <see cref="PropertySet.DaysAllowed"/>. The UI binds each
    /// ToggleButton's <c>IsChecked</c> to the chip's <c>IsSelected</c>, giving the "selected
    /// days are blue" treatment without needing a multi-binding converter.
    /// </summary>
    public IReadOnlyList<DayToggleViewModel> DaysAllowedToggles { get; }

    [ObservableProperty] private string _newTimeRange = string.Empty;

    /// <summary>
    /// True when <see cref="NewTimeRange"/> is empty or parses as <c>HH:mm-HH:mm</c>.
    /// Used by the TextBox to paint its border red on invalid input — server would fail
    /// the whole config load on a bad time range, so flagging early saves a save-then-
    /// restart debug cycle.
    /// </summary>
    public bool IsNewTimeRangeValid =>
        string.IsNullOrWhiteSpace(NewTimeRange) || TimeRangeSpec.TryParse(NewTimeRange, out _);

    partial void OnNewTimeRangeChanged(string value) => OnPropertyChanged(nameof(IsNewTimeRangeValid));
    [ObservableProperty] private string _newGroupName = string.Empty;
    [ObservableProperty] private string _newSearchTag = string.Empty;
    [ObservableProperty] private string _newExtraSectionName = string.Empty;

    [RelayCommand]
    private void SetOverride(string? propertyName) => SetHasFlag(propertyName, true);

    [RelayCommand]
    private void ClearOverride(string? propertyName) => SetHasFlag(propertyName, false);

    private void SetHasFlag(string? propertyName, bool value)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        // Wrap in an undo batch so Ctrl+Z rewinds value restoration + HasX flip in one step.
        using var _ = Project?.Undo.BeginBatch("Reset " + propertyName);

        if (!value)
        {
            // Restore the value the server would resolve to: walk the inheritance chain
            // (immediate parent → default) and take the first parent with HasX = true.
            // If none, fall back to the type's natural default (0, false, "", empty list).
            RestoreInheritedValue(propertyName);
        }

        // Assign HasX last so the value-setter's auto-set HasX=true side-effect is undone.
        var hasProp = typeof(PropertySet).GetProperty("Has" + propertyName, BindingFlags.Public | BindingFlags.Instance);
        hasProp?.SetValue(Model, value);
    }

    private void RestoreInheritedValue(string propertyName)
    {
        switch (propertyName)
        {
            case "GroupSettings":
                RestoreCollection(Model.GroupSettings, FindInheritedEnumerable<string>(propertyName));
                break;
            case "DaysAllowed":
                RestoreCollection(Model.DaysAllowed, FindInheritedEnumerable<DayOfWeek>(propertyName));
                break;
            case "AllowedTimeRanges":
                RestoreCollection(Model.AllowedTimeRanges, FindInheritedEnumerable<TimeRangeSpec>(propertyName));
                break;
            default:
                RestoreScalar(propertyName);
                break;
        }
    }

    private void RestoreScalar(string propertyName)
    {
        var valueProp = typeof(PropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (valueProp is null) return;

        object? value = FindInheritedScalar(propertyName);
        if (value is null)
        {
            // Type-level default: zero for numeric, false for bool, empty string for string.
            value = valueProp.PropertyType.IsValueType
                ? Activator.CreateInstance(valueProp.PropertyType)
                : (valueProp.PropertyType == typeof(string) ? string.Empty : null);
        }
        valueProp.SetValue(Model, value);
    }

    private object? FindInheritedScalar(string propertyName)
    {
        var chain = _inheritanceChain?.Invoke();
        if (chain is null) return null;
        foreach (var parent in chain)
        {
            if (HasFlagOn(parent, propertyName))
            {
                var vp = typeof(PropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return vp?.GetValue(parent);
            }
        }
        return null;
    }

    private IEnumerable<T>? FindInheritedEnumerable<T>(string propertyName)
    {
        var chain = _inheritanceChain?.Invoke();
        if (chain is null) return null;
        foreach (var parent in chain)
        {
            if (HasFlagOn(parent, propertyName))
            {
                var vp = typeof(PropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return vp?.GetValue(parent) as IEnumerable<T>;
            }
        }
        return null;
    }

    private static bool HasFlagOn(PropertySet target, string propertyName)
    {
        var p = typeof(PropertySet).GetProperty("Has" + propertyName, BindingFlags.Public | BindingFlags.Instance);
        return p?.GetValue(target) is bool b && b;
    }

    /// <summary>Rewrites <paramref name="coll"/> in place using iterative RemoveAt/Insert so
    /// every mutation flows through CollectionChanged and gets recorded for undo.</summary>
    private static void RestoreCollection<T>(ObservableCollection<T> coll, IEnumerable<T>? inherited)
    {
        for (var i = coll.Count - 1; i >= 0; i--) coll.RemoveAt(i);
        if (inherited is null) return;
        foreach (var item in inherited) coll.Add(item);
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

    // Reorder is now driven from the ☰ drag handle in the view; we keep this only as a
    // fallback programmatic hook (no UI binding, but handy if future keyboard support wants
    // to call it).

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

    // Reordering for GroupReferences is driven by the ☰ drag handle in the view.

    [RelayCommand]
    private void AddSearchTag()
    {
        var tag = NewSearchTag?.Trim();
        if (string.IsNullOrEmpty(tag)) return;
        using var _ = Project?.Undo.BeginBatch("Add search tag");
        if (!Model.SearchTags.Contains(tag))
            Model.SearchTags.Add(tag);
        NewSearchTag = string.Empty;
    }

    [RelayCommand]
    private void RemoveSearchTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        using var _ = Project?.Undo.BeginBatch("Remove search tag");
        Model.SearchTags.Remove(tag);
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

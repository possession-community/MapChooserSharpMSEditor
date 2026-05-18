// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

public partial class LegacyGroupRefViewModel : ObservableObject
{
    public string Name { get; }
    [ObservableProperty] private bool _isKnown;

    public LegacyGroupRefViewModel(string name, bool isKnown)
    {
        Name = name;
        _isKnown = isKnown;
    }
}

public partial class LegacyPropertySetViewModel : ViewModelBase
{
    public LegacyPropertySet Model { get; }
    public LegacyProjectContext? Project { get; }
    public LegacyPropertyScope Scope { get; }

    private readonly Func<IReadOnlyList<LegacyPropertySet>>? _inheritanceChain;

    public ObservableCollection<LegacyGroupRefViewModel> GroupReferences { get; } = new();

    public bool ShowMapNameAlias => Scope is LegacyPropertyScope.Default or LegacyPropertyScope.Map;
    public bool ShowMapDescription => Scope is LegacyPropertyScope.Default or LegacyPropertyScope.Map;
    public bool ShowWorkshopId => Scope is LegacyPropertyScope.Default or LegacyPropertyScope.Map;
    public bool ShowGroupSettings => Scope is LegacyPropertyScope.Default or LegacyPropertyScope.Map;

    public string InheritedStatusText { get; }

    public LegacyPropertySetViewModel(
        LegacyPropertySet model,
        LegacyPropertyScope scope = LegacyPropertyScope.Default,
        LegacyProjectContext? project = null,
        Func<IReadOnlyList<LegacyPropertySet>>? inheritanceChain = null,
        string inheritedStatusKey = "Status.UsingDefault")
    {
        Model = model;
        Scope = scope;
        Project = project;
        _inheritanceChain = inheritanceChain;
        InheritedStatusText = Localization.Get(inheritedStatusKey);

        RebuildGroupReferences();
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
            GroupReferences.Add(new LegacyGroupRefViewModel(name, Project?.IsKnownGroup(name) ?? true));
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

    public IReadOnlyList<DayToggleViewModel> DaysAllowedToggles { get; }

    [ObservableProperty] private string _newTimeRange = string.Empty;

    public bool IsNewTimeRangeValid =>
        string.IsNullOrWhiteSpace(NewTimeRange) || TimeRangeSpec.TryParse(NewTimeRange, out _);

    partial void OnNewTimeRangeChanged(string value) => OnPropertyChanged(nameof(IsNewTimeRangeValid));

    [ObservableProperty] private string _newGroupName = string.Empty;
    [ObservableProperty] private string _newPermission = string.Empty;
    [ObservableProperty] private string _newAllowedSteamId = string.Empty;
    [ObservableProperty] private string _newDisallowedSteamId = string.Empty;
    [ObservableProperty] private string _newExtraSectionName = string.Empty;

    [RelayCommand]
    private void SetOverride(string? propertyName) => SetHasFlag(propertyName, true);

    [RelayCommand]
    private void ClearOverride(string? propertyName) => SetHasFlag(propertyName, false);

    private void SetHasFlag(string? propertyName, bool value)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        using var _ = Project?.Undo.BeginBatch("Reset " + propertyName);

        if (!value)
            RestoreInheritedValue(propertyName);

        var hasProp = typeof(LegacyPropertySet).GetProperty("Has" + propertyName, BindingFlags.Public | BindingFlags.Instance);
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
            case "RequiredPermissions":
                RestoreCollection(Model.RequiredPermissions, FindInheritedEnumerable<string>(propertyName));
                break;
            case "AllowedSteamIds":
                RestoreCollection(Model.AllowedSteamIds, FindInheritedEnumerable<ulong>(propertyName));
                break;
            case "DisallowedSteamIds":
                RestoreCollection(Model.DisallowedSteamIds, FindInheritedEnumerable<ulong>(propertyName));
                break;
            default:
                RestoreScalar(propertyName);
                break;
        }
    }

    private void RestoreScalar(string propertyName)
    {
        var valueProp = typeof(LegacyPropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (valueProp is null) return;

        object? value = FindInheritedScalar(propertyName);
        if (value is null)
        {
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
                var vp = typeof(LegacyPropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
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
                var vp = typeof(LegacyPropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return vp?.GetValue(parent) as IEnumerable<T>;
            }
        }
        return null;
    }

    private static bool HasFlagOn(LegacyPropertySet target, string propertyName)
    {
        var p = typeof(LegacyPropertySet).GetProperty("Has" + propertyName, BindingFlags.Public | BindingFlags.Instance);
        return p?.GetValue(target) is bool b && b;
    }

    private static void RestoreCollection<T>(ObservableCollection<T> coll, IEnumerable<T>? inherited)
    {
        for (var i = coll.Count - 1; i >= 0; i--) coll.RemoveAt(i);
        if (inherited is null) return;
        foreach (var item in inherited) coll.Add(item);
    }

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
    private void RemoveGroupReference(LegacyGroupRefViewModel? reference)
    {
        if (reference is null) return;
        using var _ = Project?.Undo.BeginBatch("Remove group reference");
        Model.GroupSettings.Remove(reference.Name);
    }

    [RelayCommand]
    private void AddPermission()
    {
        var p = NewPermission?.Trim();
        if (string.IsNullOrEmpty(p)) return;
        using var _ = Project?.Undo.BeginBatch("Add permission");
        if (!Model.RequiredPermissions.Contains(p))
            Model.RequiredPermissions.Add(p);
        NewPermission = string.Empty;
    }

    [RelayCommand]
    private void RemovePermission(string? p)
    {
        if (string.IsNullOrEmpty(p)) return;
        using var _ = Project?.Undo.BeginBatch("Remove permission");
        Model.RequiredPermissions.Remove(p);
    }

    [RelayCommand]
    private void AddAllowedSteamId()
    {
        if (!ulong.TryParse(NewAllowedSteamId?.Trim(), out var id)) return;
        using var _ = Project?.Undo.BeginBatch("Add allowed SteamID");
        if (!Model.AllowedSteamIds.Contains(id))
            Model.AllowedSteamIds.Add(id);
        NewAllowedSteamId = string.Empty;
    }

    [RelayCommand]
    private void RemoveAllowedSteamId(object? boxed)
    {
        if (boxed is not ulong id) return;
        using var _ = Project?.Undo.BeginBatch("Remove allowed SteamID");
        Model.AllowedSteamIds.Remove(id);
    }

    [RelayCommand]
    private void AddDisallowedSteamId()
    {
        if (!ulong.TryParse(NewDisallowedSteamId?.Trim(), out var id)) return;
        using var _ = Project?.Undo.BeginBatch("Add disallowed SteamID");
        if (!Model.DisallowedSteamIds.Contains(id))
            Model.DisallowedSteamIds.Add(id);
        NewDisallowedSteamId = string.Empty;
    }

    [RelayCommand]
    private void RemoveDisallowedSteamId(object? boxed)
    {
        if (boxed is not ulong id) return;
        using var _ = Project?.Undo.BeginBatch("Remove disallowed SteamID");
        Model.DisallowedSteamIds.Remove(id);
    }

    [RelayCommand]
    private void AddExtraSection()
    {
        var name = NewExtraSectionName?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        using var _ = Project?.Undo.BeginBatch("Add extra section");
        Model.Extras.Add(new LegacyExtraSection { Name = name });
        NewExtraSectionName = string.Empty;
    }

    [RelayCommand]
    private void RemoveExtraSection(LegacyExtraSection section)
    {
        using var _ = Project?.Undo.BeginBatch("Remove extra section");
        Model.Extras.Remove(section);
    }

    [RelayCommand]
    private void AddExtraKey(LegacyExtraSection section)
    {
        using var _ = Project?.Undo.BeginBatch("Add extra key");
        section.Entries.Add(new LegacyExtraKeyValue { Key = "new_key", Value = string.Empty });
    }
}

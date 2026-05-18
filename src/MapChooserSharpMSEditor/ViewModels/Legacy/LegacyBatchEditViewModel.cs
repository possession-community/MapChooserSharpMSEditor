// LEGACY — remove when MCS migration completes
//
// Legacy-mode counterpart of BatchEditViewModel. Mirrors the Current implementation
// but operates on LegacyMapConfigFile / LegacyMapEntry / LegacyGroupEntry / LegacyPropertySet,
// and uses a Legacy-flavored property set (no CooldownOverride / CooldownDateTime;
// adds RestrictToAllowedUsersOnly / NominationCost / NominationSpecificCooldown).
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.Views;

namespace MapChooserSharpMSEditor.ViewModels.Legacy;

/// <summary>One row per map in the Batch Group tab (Legacy). Operates on
/// <c>Map.Properties.GroupSettings</c> — adds/removes the typed group name there.</summary>
public sealed partial class LegacyBatchMapRow : ObservableObject
{
    public LegacyMapEntry Map { get; }
    public LegacyMapConfigFile File { get; }
    public string MapName => Map.MapName;
    public string FileName => File.DisplayName;

    [ObservableProperty] private bool _isChecked = true;
    [ObservableProperty] private bool _alreadyHasGroup;

    public LegacyBatchMapRow(LegacyMapEntry map, LegacyMapConfigFile file)
    {
        Map = map;
        File = file;
    }

    public bool WillBeAdded => IsChecked && !AlreadyHasGroup;
    public bool WillBeRemoved => IsChecked && AlreadyHasGroup;

    partial void OnIsCheckedChanged(bool value)
    {
        OnPropertyChanged(nameof(WillBeAdded));
        OnPropertyChanged(nameof(WillBeRemoved));
    }
    partial void OnAlreadyHasGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(WillBeAdded));
        OnPropertyChanged(nameof(WillBeRemoved));
    }
}

/// <summary>One row per map/group in the Batch Property tab (Legacy).</summary>
public sealed partial class LegacyBatchEntryRow : ObservableObject
{
    public object Entry { get; }
    public LegacyMapConfigFile File { get; }
    public LegacyPropertySet Properties { get; }
    public string Label { get; }
    public string FileName => File.DisplayName;
    public bool IsMap { get; }

    [ObservableProperty] private bool _isChecked;

    public LegacyBatchEntryRow(LegacyMapEntry map, LegacyMapConfigFile file)
    {
        Entry = map;
        File = file;
        Properties = map.Properties;
        Label = map.MapName;
        IsMap = true;
    }

    public LegacyBatchEntryRow(LegacyGroupEntry group, LegacyMapConfigFile file)
    {
        Entry = group;
        File = file;
        Properties = group.Properties;
        Label = group.GroupName;
        IsMap = false;
    }
}

/// <summary>One editable property row in the Legacy Batch Property tab.</summary>
public sealed partial class LegacyBatchPropertyEntry : ObservableObject
{
    public string Name { get; }
    public string LocalizedLabel { get; }
    public string LocalizedDescription { get; }
    public Type ValueType { get; }

    [ObservableProperty] private bool _isIncluded;
    [ObservableProperty] private string _stringValue = string.Empty;
    [ObservableProperty] private int _intValue;
    [ObservableProperty] private long _longValue;
    [ObservableProperty] private bool _boolValue;

    public bool IsStringProp => ValueType == typeof(string);
    public bool IsIntProp => ValueType == typeof(int);
    public bool IsLongProp => ValueType == typeof(long);
    public bool IsBoolProp => ValueType == typeof(bool);

    public LegacyBatchPropertyEntry(string name, string labelLocKey, Type valueType)
    {
        Name = name;
        LocalizedLabel = Localization.Get(labelLocKey);
        LocalizedDescription = Localization.Get(labelLocKey + ".Desc");
        ValueType = valueType;
    }

    public object? CurrentValue() =>
        ValueType == typeof(string) ? StringValue
        : ValueType == typeof(int) ? IntValue
        : ValueType == typeof(long) ? LongValue
        : ValueType == typeof(bool) ? BoolValue
        : null;
}

public sealed class LegacyBatchPropertySection
{
    public string LocalizedTitle { get; }
    public string LocalizedDescription { get; }
    public ObservableCollection<LegacyBatchPropertyEntry> Entries { get; } = new();

    public LegacyBatchPropertySection(string titleLocKey)
    {
        LocalizedTitle = Localization.Get(titleLocKey);
        LocalizedDescription = Localization.Get(titleLocKey + ".Desc");
    }
}

public sealed partial class LegacyBatchEditViewModel : ViewModelBase
{
    public LegacyProjectContext Project { get; }

    /// <summary>Set by the owning view so the confirm dialog can parent itself.</summary>
    public Window? Owner { get; set; }

    // ===== Tab 1: Batch Group =====
    //
    // Operates on MAP entries — adds/removes the typed group name in each selected map's
    // Properties.GroupSettings. The group itself must be defined once elsewhere; this tab
    // is for wiring up many maps at once.

    [ObservableProperty] private string _groupName = string.Empty;
    public ObservableCollection<LegacyBatchMapRow> MapRows { get; } = new();

    [ObservableProperty] private string _groupStatusText = string.Empty;

    public int AddableCount => MapRows.Count(r => r.WillBeAdded);
    public int RemovableCount => MapRows.Count(r => r.WillBeRemoved);
    public bool CanAddGroup => !string.IsNullOrWhiteSpace(GroupName) && AddableCount > 0;
    public bool CanRemoveGroup => !string.IsNullOrWhiteSpace(GroupName) && RemovableCount > 0;

    partial void OnGroupNameChanged(string value)
    {
        RefreshMapRows();
        RaiseGroupActionState();
    }

    private void RaiseGroupActionState()
    {
        OnPropertyChanged(nameof(AddableCount));
        OnPropertyChanged(nameof(RemovableCount));
        OnPropertyChanged(nameof(CanAddGroup));
        OnPropertyChanged(nameof(CanRemoveGroup));
    }

    // ===== Tab 2: Batch Property =====

    [ObservableProperty] private BatchTargetKind _targetKind = BatchTargetKind.Maps;
    [ObservableProperty] private string _filterText = string.Empty;
    public ObservableCollection<LegacyBatchEntryRow> EntryRows { get; } = new();
    public ObservableCollection<LegacyBatchPropertySection> Sections { get; } = new();

    [ObservableProperty] private string _propertyStatusText = string.Empty;

    partial void OnTargetKindChanged(BatchTargetKind value)
    {
        RebuildSections();
        RebuildEntryRows();
    }

    partial void OnFilterTextChanged(string value) => RebuildEntryRows();

    public LegacyBatchEditViewModel(LegacyProjectContext project)
    {
        Project = project;

        foreach (var f in project.Files)
        {
            foreach (var m in f.Maps)
            {
                var row = new LegacyBatchMapRow(m, f);
                row.PropertyChanged += OnMapRowChanged;
                MapRows.Add(row);
            }
        }

        RebuildSections();
        RebuildEntryRows();
    }

    private void OnMapRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LegacyBatchMapRow.IsChecked)
                or nameof(LegacyBatchMapRow.AlreadyHasGroup)
                or nameof(LegacyBatchMapRow.WillBeAdded)
                or nameof(LegacyBatchMapRow.WillBeRemoved))
            RaiseGroupActionState();
    }

    // ───────────────────────── Tab 1 logic ─────────────────────────

    private void RefreshMapRows()
    {
        var name = GroupName?.Trim() ?? string.Empty;
        foreach (var row in MapRows)
            row.AlreadyHasGroup = !string.IsNullOrEmpty(name) &&
                row.Map.Properties.GroupSettings.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void SelectAllMaps()
    {
        foreach (var r in MapRows) r.IsChecked = true;
    }

    [RelayCommand]
    private void DeselectAllMaps()
    {
        foreach (var r in MapRows) r.IsChecked = false;
    }

    [RelayCommand]
    private void AddGroupToMaps()
    {
        var name = GroupName?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var targets = MapRows.Where(r => r.IsChecked && !r.AlreadyHasGroup).ToList();
        if (targets.Count == 0) return;

        using var batch = Project.Undo.BeginBatch("Batch add group to maps");
        foreach (var row in targets)
            row.Map.Properties.GroupSettings.Add(name);

        RefreshMapRows();
        GroupStatusText = Localization.Format("BatchEdit.GroupAdded", name, targets.Count);
    }

    [RelayCommand]
    private void RemoveGroupFromMaps()
    {
        var name = GroupName?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var targets = MapRows.Where(r => r.IsChecked && r.AlreadyHasGroup).ToList();
        if (targets.Count == 0) return;

        using var batch = Project.Undo.BeginBatch("Batch remove group from maps");
        foreach (var row in targets)
        {
            var groupList = row.Map.Properties.GroupSettings;
            for (var i = groupList.Count - 1; i >= 0; i--)
                if (string.Equals(groupList[i], name, StringComparison.OrdinalIgnoreCase))
                    groupList.RemoveAt(i);
        }

        RefreshMapRows();
        GroupStatusText = Localization.Format("BatchEdit.GroupRemoved", name, targets.Count);
    }

    // ───────────────────────── Tab 2 logic ─────────────────────────

    // Legacy-flavored descriptor set:
    //   - No CooldownOverride / CooldownDateTime (Current-only fields)
    //   - Adds RestrictToAllowedUsersOnly (Nomination) / NominationCost / NominationSpecificCooldown (Cooldown)
    private static readonly (string Name, string LocKey, Type Type, string Section, bool MapOnly, bool GroupOnly)[] AllDescriptors =
    {
        // Basic
        ("MapNameAlias",                "Prop.MapNameAlias",                     typeof(string), "Section.Basic",      true,  false),
        ("MapDescription",              "Prop.MapDescription",                   typeof(string), "Section.Basic",      true,  false),
        ("WorkshopId",                  "Prop.WorkshopId",                       typeof(long),   "Section.Basic",      true,  false),
        ("IsDisabled",                  "Prop.IsDisabled",                       typeof(bool),   "Section.Basic",      false, false),

        // Extend / Time
        ("MaxExtends",                  "Prop.MaxExtends",                       typeof(int),    "Section.ExtendTime", false, false),
        ("MaxExtCommandUses",           "Prop.MaxExtCommandUses",                typeof(int),    "Section.ExtendTime", false, false),
        ("ExtendTimePerExtends",        "Prop.ExtendTimePerExtends",             typeof(int),    "Section.ExtendTime", false, false),
        ("MapTime",                     "Prop.MapTime",                          typeof(int),    "Section.ExtendTime", false, false),
        ("ExtendRoundsPerExtends",      "Prop.ExtendRoundsPerExtends",           typeof(int),    "Section.ExtendTime", false, false),
        ("MapRounds",                   "Prop.MapRounds",                        typeof(int),    "Section.ExtendTime", false, false),

        // Nomination
        ("OnlyNomination",              "Prop.OnlyNomination",                   typeof(bool),   "Section.Nomination", false, false),
        ("MaxPlayers",                  "Prop.MaxPlayers",                       typeof(int),    "Section.Nomination", false, false),
        ("MinPlayers",                  "Prop.MinPlayers",                       typeof(int),    "Section.Nomination", false, false),
        ("ProhibitAdminNomination",     "Prop.ProhibitAdminNomination",          typeof(bool),   "Section.Nomination", false, false),
        ("RestrictToAllowedUsersOnly",  "LegacyProp.RestrictToAllowedUsersOnly", typeof(bool),   "Section.Nomination", false, false),

        // Cooldown
        ("Cooldown",                    "Prop.Cooldown",                         typeof(int),    "Section.Cooldown",   false, false),
        ("NominationCost",              "LegacyProp.NominationCost",             typeof(int),    "Section.Cooldown",   false, false),
        ("NominationSpecificCooldown",  "LegacyProp.NominationSpecificCooldown", typeof(int),    "Section.Cooldown",   false, false),
    };

    private void RebuildSections()
    {
        var prev = Sections.SelectMany(s => s.Entries).ToDictionary(e => e.Name);
        Sections.Clear();

        LegacyBatchPropertySection? current = null;
        string? currentKey = null;

        foreach (var d in AllDescriptors)
        {
            var include = TargetKind switch
            {
                BatchTargetKind.Maps => !d.GroupOnly,
                BatchTargetKind.Groups => !d.MapOnly,
                BatchTargetKind.Both => !d.MapOnly && !d.GroupOnly,
                _ => true,
            };
            if (!include) continue;

            if (currentKey != d.Section)
            {
                current = new LegacyBatchPropertySection(d.Section);
                Sections.Add(current);
                currentKey = d.Section;
            }

            LegacyBatchPropertyEntry entry;
            if (prev.TryGetValue(d.Name, out var existing) && existing.ValueType == d.Type)
                entry = existing;
            else
                entry = new LegacyBatchPropertyEntry(d.Name, d.LocKey, d.Type);

            current!.Entries.Add(entry);
        }
    }

    private void RebuildEntryRows()
    {
        var filter = FilterText?.Trim() ?? string.Empty;
        EntryRows.Clear();

        foreach (var file in Project.Files)
        {
            if (TargetKind is BatchTargetKind.Maps or BatchTargetKind.Both)
            {
                foreach (var m in file.Maps)
                    if (MatchFilter(m.MapName, filter) || MatchFilter(m.Properties.MapNameAlias, filter))
                        EntryRows.Add(new LegacyBatchEntryRow(m, file));
            }

            if (TargetKind is BatchTargetKind.Groups or BatchTargetKind.Both)
            {
                foreach (var g in file.Groups)
                    if (MatchFilter(g.GroupName, filter))
                        EntryRows.Add(new LegacyBatchEntryRow(g, file));
            }
        }
    }

    private static bool MatchFilter(string? text, string filter) =>
        string.IsNullOrEmpty(filter) ||
        (!string.IsNullOrEmpty(text) && text.Contains(filter, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private void SelectAllEntries()
    {
        foreach (var r in EntryRows) r.IsChecked = true;
    }

    [RelayCommand]
    private void DeselectAllEntries()
    {
        foreach (var r in EntryRows) r.IsChecked = false;
    }

    [RelayCommand]
    private async Task ApplyPropertyAsync()
    {
        var includedProps = Sections.SelectMany(s => s.Entries).Where(e => e.IsIncluded).ToList();
        var targets = EntryRows.Where(r => r.IsChecked).ToList();

        if (includedProps.Count == 0)
        {
            PropertyStatusText = Localization.Get("BatchEdit.NoPropertyChecked");
            return;
        }
        if (targets.Count == 0)
        {
            PropertyStatusText = Localization.Get("BatchEdit.NoSelection");
            return;
        }

        if (Owner is not null)
        {
            var propList = string.Join("\n  • ", includedProps.Select(p => p.LocalizedLabel));
            var message = Localization.Format(
                "BatchEdit.Confirm.Message", targets.Count, includedProps.Count, propList);
            var ok = await ConfirmDialog.ShowAsync(
                Owner,
                Localization.Get("BatchEdit.Confirm.Title"),
                message,
                Localization.Get("BatchEdit.Confirm.Yes"),
                Localization.Get("BatchEdit.Confirm.No"));
            if (!ok)
            {
                PropertyStatusText = Localization.Get("BatchEdit.Cancelled");
                return;
            }
        }

        var propInfos = new List<(PropertyInfo Info, LegacyBatchPropertyEntry Entry)>();
        foreach (var e in includedProps)
        {
            var pi = typeof(LegacyPropertySet).GetProperty(e.Name, BindingFlags.Public | BindingFlags.Instance);
            if (pi is not null) propInfos.Add((pi, e));
        }

        using var batch = Project.Undo.BeginBatch("Batch property edit");
        foreach (var row in targets)
            foreach (var (pi, entry) in propInfos)
                pi.SetValue(row.Properties, entry.CurrentValue());

        PropertyStatusText = Localization.Format(
            "BatchEdit.PropertiesApplied", includedProps.Count, targets.Count);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.Views;

namespace MapChooserSharpMSEditor.ViewModels;

/// <summary>Enum-to-bool converters so RadioButtons can bind directly to <see cref="BatchTargetKind"/>.</summary>
public static class BatchEditConverters
{
    public static readonly IValueConverter TargetMaps = new EnumToBoolConverter<BatchTargetKind>(BatchTargetKind.Maps);
    public static readonly IValueConverter TargetGroups = new EnumToBoolConverter<BatchTargetKind>(BatchTargetKind.Groups);
    public static readonly IValueConverter TargetBoth = new EnumToBoolConverter<BatchTargetKind>(BatchTargetKind.Both);

    private sealed class EnumToBoolConverter<T> : IValueConverter where T : struct, Enum
    {
        private readonly T _target;
        public EnumToBoolConverter(T target) => _target = target;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is T v && v.Equals(_target);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? _target : Avalonia.Data.BindingOperations.DoNothing;
    }
}

// ───────────────────────── helper types ─────────────────────────

public enum BatchTargetKind { Maps, Groups, Both }

/// <summary>One row per map in the Batch Group tab. The operation toggles whether the
/// typed group name is present in this map's <see cref="PropertySet.GroupSettings"/>.</summary>
public sealed partial class BatchMapRow : ObservableObject
{
    public MapEntryModel Map { get; }
    public MapConfigFile File { get; }
    public string MapName => Map.MapName;
    public string FileName => File.DisplayName;

    [ObservableProperty] private bool _isChecked = true;
    [ObservableProperty] private bool _alreadyHasGroup;

    public BatchMapRow(MapEntryModel map, MapConfigFile file)
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

/// <summary>One row per map/group in the Batch Property tab.</summary>
public sealed partial class BatchEntryRow : ObservableObject
{
    public object Entry { get; }
    public MapConfigFile File { get; }
    public PropertySet Properties { get; }
    public string Label { get; }
    public string FileName => File.DisplayName;
    public bool IsMap { get; }

    [ObservableProperty] private bool _isChecked;

    public BatchEntryRow(MapEntryModel map, MapConfigFile file)
    {
        Entry = map;
        File = file;
        Properties = map.Properties;
        Label = map.MapName;
        IsMap = true;
    }

    public BatchEntryRow(GroupEntryModel group, MapConfigFile file)
    {
        Entry = group;
        File = file;
        Properties = group.Properties;
        Label = group.GroupName;
        IsMap = false;
    }
}

/// <summary>One editable property row in the Batch Property tab. Owns its own typed value
/// and an "include" checkbox that gates whether ApplyProperty writes this property.</summary>
public sealed partial class BatchPropertyEntry : ObservableObject
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

    public BatchPropertyEntry(string name, string labelLocKey, Type valueType)
    {
        Name = name;
        LocalizedLabel = Localization.Get(labelLocKey);
        LocalizedDescription = Localization.Get(labelLocKey + ".Desc");
        ValueType = valueType;
    }

    /// <summary>Returns the currently-set value boxed in its actual type, or null if the
    /// type isn't one of the four supported scalar kinds.</summary>
    public object? CurrentValue() =>
        ValueType == typeof(string) ? StringValue
        : ValueType == typeof(int) ? IntValue
        : ValueType == typeof(long) ? LongValue
        : ValueType == typeof(bool) ? BoolValue
        : null;
}

/// <summary>One UI section grouping related property entries (Basic / Extend / Nomination / Cooldown).</summary>
public sealed class BatchPropertySection
{
    public string LocalizedTitle { get; }
    public string LocalizedDescription { get; }
    public ObservableCollection<BatchPropertyEntry> Entries { get; } = new();

    public BatchPropertySection(string titleLocKey)
    {
        LocalizedTitle = Localization.Get(titleLocKey);
        LocalizedDescription = Localization.Get(titleLocKey + ".Desc");
    }
}

// ───────────────────────── main VM ─────────────────────────

public sealed partial class BatchEditViewModel : ViewModelBase
{
    public ProjectContext Project { get; }

    /// <summary>Set by the owning view (MainWindowViewModel) so confirmation dialogs can
    /// parent themselves correctly. Null fallback skips the confirm step.</summary>
    public Window? Owner { get; set; }

    // ===== Tab 1: Batch Group =====
    //
    // Operates on MAP entries (not on file-level group definitions). For each selected map,
    // adding inserts the typed name into map.Properties.GroupSettings; removing pulls it out.
    // This matches how users actually attach a group to maps — the group itself is defined
    // once elsewhere, and the batch tab is for wiring up many maps at once.

    [ObservableProperty] private string _groupName = string.Empty;
    public ObservableCollection<BatchMapRow> MapRows { get; } = new();

    [ObservableProperty] private string _groupStatusText = string.Empty;

    /// <summary>Impact count: maps the Add button would actually touch (checked AND missing).</summary>
    public int AddableCount => MapRows.Count(r => r.WillBeAdded);
    /// <summary>Impact count: maps the Remove button would actually touch (checked AND has).</summary>
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
    public ObservableCollection<BatchEntryRow> EntryRows { get; } = new();

    /// <summary>Sections of property rows, filtered by the current TargetKind.</summary>
    public ObservableCollection<BatchPropertySection> Sections { get; } = new();

    [ObservableProperty] private string _propertyStatusText = string.Empty;

    partial void OnTargetKindChanged(BatchTargetKind value)
    {
        RebuildSections();
        RebuildEntryRows();
    }

    partial void OnFilterTextChanged(string value) => RebuildEntryRows();

    // ───────────────────────── construction ─────────────────────────

    public BatchEditViewModel(ProjectContext project)
    {
        Project = project;

        foreach (var f in project.Files)
        {
            foreach (var m in f.Maps)
            {
                var row = new BatchMapRow(m, f);
                row.PropertyChanged += OnMapRowChanged;
                MapRows.Add(row);
            }
        }

        RebuildSections();
        RebuildEntryRows();
    }

    private void OnMapRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BatchMapRow.IsChecked)
                or nameof(BatchMapRow.AlreadyHasGroup)
                or nameof(BatchMapRow.WillBeAdded)
                or nameof(BatchMapRow.WillBeRemoved))
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
            // Strip every occurrence — defensive in case the group was somehow
            // listed more than once on a single map.
            var groupList = row.Map.Properties.GroupSettings;
            for (var i = groupList.Count - 1; i >= 0; i--)
                if (string.Equals(groupList[i], name, StringComparison.OrdinalIgnoreCase))
                    groupList.RemoveAt(i);
        }

        RefreshMapRows();
        GroupStatusText = Localization.Format("BatchEdit.GroupRemoved", name, targets.Count);
    }

    // ───────────────────────── Tab 2 logic ─────────────────────────

    // Each descriptor is (Name, LabelLocKey, ValueType, SectionKey, MapOnly, GroupOnly).
    // Section keys map to the existing Section.* localization keys so the headers match the
    // regular property editor.
    private static readonly (string Name, string LocKey, Type Type, string Section, bool MapOnly, bool GroupOnly)[] AllDescriptors =
    {
        // Basic
        ("MapNameAlias",           "Prop.MapNameAlias",           typeof(string), "Section.Basic",       true,  false),
        ("MapDescription",         "Prop.MapDescription",         typeof(string), "Section.Basic",       true,  false),
        ("WorkshopId",             "Prop.WorkshopId",             typeof(long),   "Section.Basic",       true,  false),
        ("IsDisabled",             "Prop.IsDisabled",             typeof(bool),   "Section.Basic",       false, false),
        ("CooldownOverride",       "Prop.CooldownOverride",       typeof(int),    "Section.Basic",       false, true),
        ("ShortGroupName",         "Prop.ShortGroupName",         typeof(string), "Section.Basic",       false, true),
        ("NominationLimit",        "Prop.NominationLimit",        typeof(int),    "Section.Basic",       false, true),

        // Extend / Time
        ("MaxExtends",             "Prop.MaxExtends",             typeof(int),    "Section.ExtendTime",  false, false),
        ("MaxExtCommandUses",      "Prop.MaxExtCommandUses",      typeof(int),    "Section.ExtendTime",  false, false),
        ("ExtendTimePerExtends",   "Prop.ExtendTimePerExtends",   typeof(int),    "Section.ExtendTime",  false, false),
        ("MapTime",                "Prop.MapTime",                typeof(int),    "Section.ExtendTime",  false, false),
        ("ExtendRoundsPerExtends", "Prop.ExtendRoundsPerExtends", typeof(int),    "Section.ExtendTime",  false, false),
        ("MapRounds",              "Prop.MapRounds",              typeof(int),    "Section.ExtendTime",  false, false),

        // Nomination
        ("MapSelectionWeight",     "Prop.MapSelectionWeight",     typeof(int),    "Section.Nomination",  false, false),
        ("OnlyNomination",         "Prop.OnlyNomination",         typeof(bool),   "Section.Nomination",  false, false),
        ("MaxPlayers",             "Prop.MaxPlayers",             typeof(int),    "Section.Nomination",  false, false),
        ("MinPlayers",             "Prop.MinPlayers",             typeof(int),    "Section.Nomination",  false, false),
        ("ProhibitAdminNomination","Prop.ProhibitAdminNomination",typeof(bool),   "Section.Nomination",  false, false),

        // Cooldown
        ("Cooldown",               "Prop.Cooldown",               typeof(int),    "Section.Cooldown",    false, false),
        ("CooldownDateTime",       "Prop.CooldownDateTime",       typeof(string), "Section.Cooldown",    false, false),
        ("NominationCooldown",     "Prop.NominationCooldown",     typeof(int),    "Section.Cooldown",    false, false),
        ("NominationCooldownDateTime","Prop.NominationCooldownDateTime",typeof(string),"Section.Cooldown",false, false),
    };

    private void RebuildSections()
    {
        // Preserve the user's include/value state across target-kind switches by keying on
        // property name. Anything still relevant after the filter keeps its row state.
        var prev = Sections.SelectMany(s => s.Entries).ToDictionary(e => e.Name);
        Sections.Clear();

        BatchPropertySection? current = null;
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
                current = new BatchPropertySection(d.Section);
                Sections.Add(current);
                currentKey = d.Section;
            }

            BatchPropertyEntry entry;
            if (prev.TryGetValue(d.Name, out var existing) && existing.ValueType == d.Type)
                entry = existing;
            else
                entry = new BatchPropertyEntry(d.Name, d.LocKey, d.Type);

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
                        EntryRows.Add(new BatchEntryRow(m, file));
            }

            if (TargetKind is BatchTargetKind.Groups or BatchTargetKind.Both)
            {
                foreach (var g in file.Groups)
                    if (MatchFilter(g.GroupName, filter))
                        EntryRows.Add(new BatchEntryRow(g, file));
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

        // Cache PropertyInfo lookups so reflection only fires once per included property.
        var propInfos = new List<(PropertyInfo Info, BatchPropertyEntry Entry)>();
        foreach (var e in includedProps)
        {
            var pi = typeof(PropertySet).GetProperty(e.Name, BindingFlags.Public | BindingFlags.Instance);
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

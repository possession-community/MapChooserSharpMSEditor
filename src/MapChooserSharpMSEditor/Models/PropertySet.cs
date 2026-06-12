using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models;

/// <summary>
/// Editable set of TOML properties. Every property has a HasX flag so unset fields can be
/// omitted from the written TOML (mirroring the server's "null = not specified" semantics).
///
/// Auto-set semantics: touching any value flips its paired HasX to true. This lives on the
/// model itself (not the view-model) so the auto-flip happens <i>inside</i> the setter's
/// PropertyChanging/PropertyChanged span, which lets the undo system batch the value
/// change and the flag flip into a single Ctrl+Z step.
/// </summary>
public partial class PropertySet : ObservableObject
{
    public PropertySet()
    {
        // Mutating a collection counts as "setting" that property — flip HasX to true so it
        // emits to TOML. Put here (not in the VM) so undo's depth counter sees the flag flip
        // as part of the same stack frame as the collection change.
        GroupSettings.CollectionChanged += (_, _) => HasGroupSettings = true;
        DaysAllowed.CollectionChanged += (_, _) => HasDaysAllowed = true;
        AllowedTimeRanges.CollectionChanged += (_, _) => HasAllowedTimeRanges = true;
    }

    // Scalar auto-set: touching a value flips HasX. Nested HasX set happens inside the outer
    // setter's span, so both changes share one auto-batch in UndoManager.
    partial void OnMapNameAliasChanged(string value) => HasMapNameAlias = true;
    partial void OnMapDescriptionChanged(string value) => HasMapDescription = true;
    partial void OnWorkshopIdChanged(long value) => HasWorkshopId = true;
    partial void OnIsDisabledChanged(bool value) => HasIsDisabled = true;
    partial void OnCooldownOverrideChanged(int value) => HasCooldownOverride = true;
    partial void OnMaxExtendsChanged(int value) => HasMaxExtends = true;
    partial void OnMaxExtCommandUsesChanged(int value) => HasMaxExtCommandUses = true;
    partial void OnExtendTimePerExtendsChanged(int value) => HasExtendTimePerExtends = true;
    partial void OnMapTimeChanged(int value) => HasMapTime = true;
    partial void OnExtendRoundsPerExtendsChanged(int value) => HasExtendRoundsPerExtends = true;
    partial void OnMapRoundsChanged(int value) => HasMapRounds = true;
    partial void OnOnlyNominationChanged(bool value) => HasOnlyNomination = true;
    partial void OnMaxPlayersChanged(int value) => HasMaxPlayers = true;
    partial void OnMinPlayersChanged(int value) => HasMinPlayers = true;
    partial void OnProhibitAdminNominationChanged(bool value) => HasProhibitAdminNomination = true;
    partial void OnCooldownChanged(int value) => HasCooldown = true;
    partial void OnCooldownDateTimeChanged(string value) => HasCooldownDateTime = true;
    partial void OnNominationCooldownChanged(int value) => HasNominationCooldown = true;
    partial void OnNominationCooldownDateTimeChanged(string value) => HasNominationCooldownDateTime = true;

    // ===== Basic =====
    [ObservableProperty] private bool _hasMapNameAlias;
    [ObservableProperty] private string _mapNameAlias = string.Empty;

    [ObservableProperty] private bool _hasMapDescription;
    [ObservableProperty] private string _mapDescription = string.Empty;

    [ObservableProperty] private bool _hasWorkshopId;
    [ObservableProperty] private long _workshopId;

    [ObservableProperty] private bool _hasIsDisabled;
    [ObservableProperty] private bool _isDisabled;

    // Map-only: which group names to inherit from
    [ObservableProperty] private bool _hasGroupSettings;
    public ObservableCollection<string> GroupSettings { get; } = new();

    // Group-only: overrides the cooldown of any map that references this group
    [ObservableProperty] private bool _hasCooldownOverride;
    [ObservableProperty] private int _cooldownOverride;

    // ===== Extend =====
    [ObservableProperty] private bool _hasMaxExtends;
    [ObservableProperty] private int _maxExtends;

    [ObservableProperty] private bool _hasMaxExtCommandUses;
    [ObservableProperty] private int _maxExtCommandUses;

    [ObservableProperty] private bool _hasExtendTimePerExtends;
    [ObservableProperty] private int _extendTimePerExtends;

    [ObservableProperty] private bool _hasMapTime;
    [ObservableProperty] private int _mapTime;

    [ObservableProperty] private bool _hasExtendRoundsPerExtends;
    [ObservableProperty] private int _extendRoundsPerExtends;

    [ObservableProperty] private bool _hasMapRounds;
    [ObservableProperty] private int _mapRounds;

    // ===== Nomination / pick =====
    [ObservableProperty] private bool _hasOnlyNomination;
    [ObservableProperty] private bool _onlyNomination;

    [ObservableProperty] private bool _hasMaxPlayers;
    [ObservableProperty] private int _maxPlayers;

    [ObservableProperty] private bool _hasMinPlayers;
    [ObservableProperty] private int _minPlayers;

    [ObservableProperty] private bool _hasProhibitAdminNomination;
    [ObservableProperty] private bool _prohibitAdminNomination;

    [ObservableProperty] private bool _hasDaysAllowed;
    public ObservableCollection<DayOfWeek> DaysAllowed { get; } = new();

    [ObservableProperty] private bool _hasAllowedTimeRanges;
    public ObservableCollection<TimeRangeSpec> AllowedTimeRanges { get; } = new();

    // ===== Cooldown =====
    [ObservableProperty] private bool _hasCooldown;
    [ObservableProperty] private int _cooldown;

    [ObservableProperty] private bool _hasCooldownDateTime;
    [ObservableProperty] private string _cooldownDateTime = string.Empty;

    [ObservableProperty] private bool _hasNominationCooldown;
    [ObservableProperty] private int _nominationCooldown;

    [ObservableProperty] private bool _hasNominationCooldownDateTime;
    [ObservableProperty] private string _nominationCooldownDateTime = string.Empty;

    // ===== Extra sub-tables (custom external plugin data) =====
    public ObservableCollection<ExtraSection> Extras { get; } = new();
}

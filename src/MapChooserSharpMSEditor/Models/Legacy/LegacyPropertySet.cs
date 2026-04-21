// LEGACY — remove when MCS migration completes
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models.Legacy;

/// <summary>
/// Legacy MapChooserSharp.API TOML property bag. Mirrors PropertySet but with the
/// Legacy-specific fields (RestrictToAllowedUsersOnly / RequiredPermissions / SteamIds /
/// NominationCost / NominationSpecificCooldown) and without Current-only ones
/// (CooldownDateTime, CooldownOverride). Extras are flat string key-value pairs (no typed
/// values) to match the API's <c>Dictionary&lt;string, Dictionary&lt;string, string&gt;&gt;</c>.
/// </summary>
public partial class LegacyPropertySet : ObservableObject
{
    public LegacyPropertySet()
    {
        GroupSettings.CollectionChanged += (_, _) => HasGroupSettings = true;
        DaysAllowed.CollectionChanged += (_, _) => HasDaysAllowed = true;
        AllowedTimeRanges.CollectionChanged += (_, _) => HasAllowedTimeRanges = true;
        RequiredPermissions.CollectionChanged += (_, _) => HasRequiredPermissions = true;
        AllowedSteamIds.CollectionChanged += (_, _) => HasAllowedSteamIds = true;
        DisallowedSteamIds.CollectionChanged += (_, _) => HasDisallowedSteamIds = true;
    }

    partial void OnMapNameAliasChanged(string value) => HasMapNameAlias = true;
    partial void OnMapDescriptionChanged(string value) => HasMapDescription = true;
    partial void OnWorkshopIdChanged(long value) => HasWorkshopId = true;
    partial void OnIsDisabledChanged(bool value) => HasIsDisabled = true;
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
    partial void OnRestrictToAllowedUsersOnlyChanged(bool value) => HasRestrictToAllowedUsersOnly = true;
    partial void OnCooldownChanged(int value) => HasCooldown = true;
    partial void OnNominationCostChanged(int value) => HasNominationCost = true;
    partial void OnNominationSpecificCooldownChanged(int value) => HasNominationSpecificCooldown = true;

    // ===== Basic =====
    [ObservableProperty] private bool _hasMapNameAlias;
    [ObservableProperty] private string _mapNameAlias = string.Empty;

    [ObservableProperty] private bool _hasMapDescription;
    [ObservableProperty] private string _mapDescription = string.Empty;

    [ObservableProperty] private bool _hasWorkshopId;
    [ObservableProperty] private long _workshopId;

    [ObservableProperty] private bool _hasIsDisabled;
    [ObservableProperty] private bool _isDisabled;

    [ObservableProperty] private bool _hasGroupSettings;
    public ObservableCollection<string> GroupSettings { get; } = new();

    // ===== Extend / Time =====
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

    // ===== Nomination =====
    [ObservableProperty] private bool _hasOnlyNomination;
    [ObservableProperty] private bool _onlyNomination;

    [ObservableProperty] private bool _hasMaxPlayers;
    [ObservableProperty] private int _maxPlayers;

    [ObservableProperty] private bool _hasMinPlayers;
    [ObservableProperty] private int _minPlayers;

    [ObservableProperty] private bool _hasProhibitAdminNomination;
    [ObservableProperty] private bool _prohibitAdminNomination;

    [ObservableProperty] private bool _hasRestrictToAllowedUsersOnly;
    [ObservableProperty] private bool _restrictToAllowedUsersOnly;

    [ObservableProperty] private bool _hasRequiredPermissions;
    public ObservableCollection<string> RequiredPermissions { get; } = new();

    [ObservableProperty] private bool _hasAllowedSteamIds;
    public ObservableCollection<ulong> AllowedSteamIds { get; } = new();

    [ObservableProperty] private bool _hasDisallowedSteamIds;
    public ObservableCollection<ulong> DisallowedSteamIds { get; } = new();

    [ObservableProperty] private bool _hasDaysAllowed;
    public ObservableCollection<DayOfWeek> DaysAllowed { get; } = new();

    [ObservableProperty] private bool _hasAllowedTimeRanges;
    public ObservableCollection<TimeRangeSpec> AllowedTimeRanges { get; } = new();

    // ===== Cooldown =====
    [ObservableProperty] private bool _hasCooldown;
    [ObservableProperty] private int _cooldown;

    [ObservableProperty] private bool _hasNominationCost;
    [ObservableProperty] private int _nominationCost;

    [ObservableProperty] private bool _hasNominationSpecificCooldown;
    [ObservableProperty] private int _nominationSpecificCooldown;

    // ===== Extras (string-only) =====
    public ObservableCollection<LegacyExtraSection> Extras { get; } = new();
}

// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels.Legacy;

public sealed partial class LegacySearchViewModel : ViewModelBase
{
    private const int MaxResults = 100;

    private readonly ViewModels.MainWindowViewModel _main;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _includeDefaults = true;
    [ObservableProperty] private bool _includeGroups = true;
    [ObservableProperty] private bool _includeMaps = true;

    [ObservableProperty] private bool _isAdvancedOpen;

    [ObservableProperty] private string _groupFilter = "";
    [ObservableProperty] private string _aliasFilter = "";
    [ObservableProperty] private string _descriptionFilter = "";
    [ObservableProperty] private string _fileNameFilter = "";

    [ObservableProperty] private string _workshopIdFilter = "";
    [ObservableProperty] private NumericOp _cooldownOp = NumericOp.Any;
    [ObservableProperty] private string _cooldownValue = "";
    [ObservableProperty] private NumericOp _maxPlayersOp = NumericOp.Any;
    [ObservableProperty] private string _maxPlayersValue = "";
    [ObservableProperty] private NumericOp _minPlayersOp = NumericOp.Any;
    [ObservableProperty] private string _minPlayersValue = "";
    [ObservableProperty] private NumericOp _mapTimeOp = NumericOp.Any;
    [ObservableProperty] private string _mapTimeValue = "";
    [ObservableProperty] private NumericOp _mapRoundsOp = NumericOp.Any;
    [ObservableProperty] private string _mapRoundsValue = "";

    [ObservableProperty] private TriState _disabledFilter = TriState.Any;
    [ObservableProperty] private TriState _onlyNominationFilter = TriState.Any;
    [ObservableProperty] private TriState _prohibitAdminFilter = TriState.Any;

    [ObservableProperty] private DayOfWeek? _dayFilter;
    [ObservableProperty] private string _extrasKeyFilter = "";
    [ObservableProperty] private string _extrasValueFilter = "";

    public ObservableCollection<LegacySearchResult> Results { get; } = new();

    public static NumericOp[] AllNumericOps { get; } =
        { NumericOp.Any, NumericOp.Eq, NumericOp.Gt, NumericOp.Ge, NumericOp.Lt, NumericOp.Le };

    public static TriState[] AllTriStates { get; } = { TriState.Any, TriState.Yes, TriState.No };

    public static DayOfWeek?[] AllDayOptions { get; } =
    {
        null, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    };

    public bool HasResults => Results.Count > 0;
    public bool ShowNoResults =>
        (HasAnyFilter() || !string.IsNullOrWhiteSpace(Query)) && Results.Count == 0;

    public LegacySearchViewModel(ViewModels.MainWindowViewModel main)
    {
        _main = main;
        _main.LegacyProject.Files.CollectionChanged += (_, _) => Refresh();
        Results.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowNoResults));
        };
    }

    partial void OnQueryChanged(string value) { Refresh(); OnPropertyChanged(nameof(ShowNoResults)); }
    partial void OnIncludeDefaultsChanged(bool value) => Refresh();
    partial void OnIncludeGroupsChanged(bool value) => Refresh();
    partial void OnIncludeMapsChanged(bool value) => Refresh();
    partial void OnGroupFilterChanged(string value) => Refresh();
    partial void OnAliasFilterChanged(string value) => Refresh();
    partial void OnDescriptionFilterChanged(string value) => Refresh();
    partial void OnFileNameFilterChanged(string value) => Refresh();
    partial void OnWorkshopIdFilterChanged(string value) => Refresh();
    partial void OnCooldownOpChanged(NumericOp value) => Refresh();
    partial void OnCooldownValueChanged(string value) => Refresh();
    partial void OnMaxPlayersOpChanged(NumericOp value) => Refresh();
    partial void OnMaxPlayersValueChanged(string value) => Refresh();
    partial void OnMinPlayersOpChanged(NumericOp value) => Refresh();
    partial void OnMinPlayersValueChanged(string value) => Refresh();
    partial void OnMapTimeOpChanged(NumericOp value) => Refresh();
    partial void OnMapTimeValueChanged(string value) => Refresh();
    partial void OnMapRoundsOpChanged(NumericOp value) => Refresh();
    partial void OnMapRoundsValueChanged(string value) => Refresh();
    partial void OnDisabledFilterChanged(TriState value) => Refresh();
    partial void OnOnlyNominationFilterChanged(TriState value) => Refresh();
    partial void OnProhibitAdminFilterChanged(TriState value) => Refresh();
    partial void OnDayFilterChanged(DayOfWeek? value) => Refresh();
    partial void OnExtrasKeyFilterChanged(string value) => Refresh();
    partial void OnExtrasValueFilterChanged(string value) => Refresh();

    [RelayCommand]
    private void ToggleAdvanced() => IsAdvancedOpen = !IsAdvancedOpen;

    [RelayCommand]
    private void ResetAdvanced()
    {
        GroupFilter = AliasFilter = DescriptionFilter = FileNameFilter = "";
        WorkshopIdFilter = "";
        CooldownOp = MaxPlayersOp = MinPlayersOp = MapTimeOp = MapRoundsOp = NumericOp.Any;
        CooldownValue = MaxPlayersValue = MinPlayersValue = MapTimeValue = MapRoundsValue = "";
        DisabledFilter = OnlyNominationFilter = ProhibitAdminFilter = TriState.Any;
        DayFilter = null;
        ExtrasKeyFilter = ExtrasValueFilter = "";
    }

    private bool HasAnyFilter() =>
        !string.IsNullOrWhiteSpace(GroupFilter) ||
        !string.IsNullOrWhiteSpace(AliasFilter) ||
        !string.IsNullOrWhiteSpace(DescriptionFilter) ||
        !string.IsNullOrWhiteSpace(FileNameFilter) ||
        !string.IsNullOrWhiteSpace(WorkshopIdFilter) ||
        CooldownOp != NumericOp.Any ||
        MaxPlayersOp != NumericOp.Any ||
        MinPlayersOp != NumericOp.Any ||
        MapTimeOp != NumericOp.Any ||
        MapRoundsOp != NumericOp.Any ||
        DisabledFilter != TriState.Any ||
        OnlyNominationFilter != TriState.Any ||
        ProhibitAdminFilter != TriState.Any ||
        DayFilter is not null ||
        !string.IsNullOrWhiteSpace(ExtrasKeyFilter) ||
        !string.IsNullOrWhiteSpace(ExtrasValueFilter);

    private void Refresh()
    {
        Results.Clear();
        var tokens = SearchQuery.Tokenize(Query);
        if (tokens.Count == 0 && !HasAnyFilter()) return;

        Log.Debug("LegacySearch", $"Refresh tokens=[{string.Join(", ", tokens)}], advanced={HasAnyFilter()}");

        if (IncludeDefaults && _main.LegacyProject.DefaultOwner is { DefaultSettings: { } def } owner)
        {
            var cand = new Candidate(LegacySearchResultKind.Default, new[] { "Default", owner.DisplayName }, owner, def);
            if (Matches(tokens, cand))
                Add(new LegacySearchResult(LegacySearchResultKind.Default, Localization.Get("Search.Kind.Default"), owner.DisplayName, owner, owner));
        }

        foreach (var file in _main.LegacyProject.Files)
        {
            if (IncludeGroups)
            {
                foreach (var g in file.Groups)
                {
                    var cand = new Candidate(LegacySearchResultKind.Group, new[] { g.GroupName }, file, g.Properties, GroupName: g.GroupName);
                    if (Matches(tokens, cand))
                        Add(new LegacySearchResult(LegacySearchResultKind.Group, g.GroupName, file.DisplayName, file, g));
                }
            }
            if (IncludeMaps)
            {
                foreach (var m in file.Maps)
                {
                    var cand = new Candidate(LegacySearchResultKind.Map, new[] { m.MapName, m.Properties.MapNameAlias }, file, m.Properties, MapName: m.MapName);
                    if (Matches(tokens, cand))
                        Add(new LegacySearchResult(LegacySearchResultKind.Map, m.MapName, file.DisplayName, file, m));
                }
            }
            if (Results.Count >= MaxResults) break;
        }
    }

    private void Add(LegacySearchResult r)
    {
        if (Results.Count < MaxResults) Results.Add(r);
    }

    private bool Matches(IReadOnlyList<string> tokens, Candidate c)
    {
        foreach (var tok in tokens)
        {
            var hit = false;
            foreach (var h in c.DisplayText)
                if (!string.IsNullOrEmpty(h) && h.Contains(tok, StringComparison.OrdinalIgnoreCase))
                { hit = true; break; }
            if (!hit) return false;
        }

        if (!string.IsNullOrWhiteSpace(GroupFilter) && !MatchGroup(c, GroupFilter)) return false;
        if (!string.IsNullOrWhiteSpace(AliasFilter) && !Contains(c.Props?.MapNameAlias, AliasFilter)) return false;
        if (!string.IsNullOrWhiteSpace(DescriptionFilter) && !Contains(c.Props?.MapDescription, DescriptionFilter)) return false;
        if (!string.IsNullOrWhiteSpace(FileNameFilter) && !MatchFile(c, FileNameFilter)) return false;

        if (!string.IsNullOrWhiteSpace(WorkshopIdFilter))
        {
            var target = SearchNumericOps.TryParseLong(WorkshopIdFilter);
            if (target is null) return false;
            if (c.Props?.HasWorkshopId != true || c.Props!.WorkshopId != target) return false;
        }
        if (!Numeric(CooldownOp, CooldownValue, c.Props?.HasCooldown, c.Props?.Cooldown)) return false;
        if (!Numeric(MaxPlayersOp, MaxPlayersValue, c.Props?.HasMaxPlayers, c.Props?.MaxPlayers)) return false;
        if (!Numeric(MinPlayersOp, MinPlayersValue, c.Props?.HasMinPlayers, c.Props?.MinPlayers)) return false;
        if (!Numeric(MapTimeOp, MapTimeValue, c.Props?.HasMapTime, c.Props?.MapTime)) return false;
        if (!Numeric(MapRoundsOp, MapRoundsValue, c.Props?.HasMapRounds, c.Props?.MapRounds)) return false;

        if (!Tri(DisabledFilter, c.Props?.HasIsDisabled == true ? c.Props!.IsDisabled : (bool?)null)) return false;
        if (!Tri(OnlyNominationFilter, c.Props?.HasOnlyNomination == true ? c.Props!.OnlyNomination : (bool?)null)) return false;
        if (!Tri(ProhibitAdminFilter, c.Props?.HasProhibitAdminNomination == true ? c.Props!.ProhibitAdminNomination : (bool?)null)) return false;

        if (DayFilter is { } dow)
        {
            if (c.Props is null || !c.Props.DaysAllowed.Contains(dow)) return false;
        }
        if (!string.IsNullOrWhiteSpace(ExtrasKeyFilter) || !string.IsNullOrWhiteSpace(ExtrasValueFilter))
        {
            if (!MatchExtras(c, ExtrasKeyFilter, ExtrasValueFilter)) return false;
        }

        return true;
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool MatchGroup(Candidate c, string needle)
    {
        if (c.GroupName is { } gn && gn.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;
        if (c.Props is null) return false;
        foreach (var g in c.Props.GroupSettings)
            if (g.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool MatchFile(Candidate c, string needle)
    {
        if (c.File.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
        if (c.File.FilePath is { } fp && Path.GetFileName(fp).Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool Numeric(NumericOp op, string text, bool? hasFlag, int? value)
    {
        if (op == NumericOp.Any) return true;
        var target = SearchNumericOps.TryParseLong(text);
        if (target is null) return true;
        if (hasFlag != true || value is null) return false;
        return SearchNumericOps.Compare(value.Value, op, target.Value);
    }

    private static bool Tri(TriState filter, bool? actual)
    {
        if (filter == TriState.Any) return true;
        if (actual is null) return false;
        return (filter == TriState.Yes) == actual.Value;
    }

    private static bool MatchExtras(Candidate c, string? keyFilter, string? valueFilter)
    {
        if (c.Props is null) return false;
        foreach (var sec in c.Props.Extras)
            foreach (var kv in sec.Entries)
            {
                var keyOk = string.IsNullOrWhiteSpace(keyFilter)
                    || kv.Key.Contains(keyFilter, StringComparison.OrdinalIgnoreCase);
                var valOk = string.IsNullOrWhiteSpace(valueFilter)
                    || kv.Value.Contains(valueFilter, StringComparison.OrdinalIgnoreCase);
                if (keyOk && valOk) return true;
            }
        return false;
    }

    private sealed record Candidate(
        LegacySearchResultKind Kind,
        string[] DisplayText,
        LegacyMapConfigFile File,
        LegacyPropertySet? Props,
        string? GroupName = null,
        string? MapName = null);

    [RelayCommand]
    private void Open(LegacySearchResult? result)
    {
        if (result is null) return;
        _main.LegacyNavigateToSearchResult(result);
    }

    [RelayCommand]
    private void OpenTop()
    {
        if (Results.Count > 0) Open(Results[0]);
    }

    [RelayCommand]
    private void Close() => _main.CloseLegacySearchWindow();
}

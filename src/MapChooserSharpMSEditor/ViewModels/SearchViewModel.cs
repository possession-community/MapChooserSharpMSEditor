using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels;

/// <summary>
/// Quick-find for the workspace. Two independent filter layers, combined with AND:
/// <list type="bullet">
///   <item><see cref="Query"/> — whitespace-separated tokens, every token must appear in
///   the candidate's display text (name / alias / group / map / override).</item>
///   <item>Advanced form inputs (Group/Workshop/Cooldown/etc.) — typed predicates that
///   operate against the candidate's resolved PropertySet. Each filter self-skips when
///   empty, so users only pay for the filters they actually set.</item>
/// </list>
/// The <c>IncludeX</c> checkboxes still act as a category short-circuit for restricting
/// result shape without filling in fields.
/// </summary>
public sealed partial class SearchViewModel : ViewModelBase
{
    private const int MaxResults = 100;

    private readonly MainWindowViewModel _main;

    // ----- Main query + category toggles -----
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _includeDefaults = true;
    [ObservableProperty] private bool _includeGroups = true;
    [ObservableProperty] private bool _includeMaps = true;
    [ObservableProperty] private bool _includeOverrides = true;

    // ----- Advanced panel toggle -----
    [ObservableProperty] private bool _isAdvancedOpen;

    // ----- Advanced: text filters -----
    [ObservableProperty] private string _groupFilter = "";
    [ObservableProperty] private string _aliasFilter = "";
    [ObservableProperty] private string _descriptionFilter = "";
    [ObservableProperty] private string _fileNameFilter = "";

    // ----- Advanced: numeric filters (op + value) -----
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

    // ----- Advanced: tri-state flags -----
    [ObservableProperty] private TriState _disabledFilter = TriState.Any;
    [ObservableProperty] private TriState _onlyNominationFilter = TriState.Any;
    [ObservableProperty] private TriState _prohibitAdminFilter = TriState.Any;

    // ----- Advanced: day / extras -----
    [ObservableProperty] private DayOfWeek? _dayFilter;
    [ObservableProperty] private string _extrasKeyFilter = "";
    [ObservableProperty] private string _extrasValueFilter = "";

    public ObservableCollection<SearchResult> Results { get; } = new();

    /// <summary>Values for the numeric-op ComboBoxes.</summary>
    public static NumericOp[] AllNumericOps { get; } =
        { NumericOp.Any, NumericOp.Eq, NumericOp.Gt, NumericOp.Ge, NumericOp.Lt, NumericOp.Le };

    public static TriState[] AllTriStates { get; } = { TriState.Any, TriState.Yes, TriState.No };

    /// <summary>"Any" entry first, then the real days; Day? null becomes "Any" in the UI.</summary>
    public static DayOfWeek?[] AllDayOptions { get; } =
    {
        null, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    };

    public bool HasResults => Results.Count > 0;
    public bool ShowNoResults =>
        (HasAnyFilter() || !string.IsNullOrWhiteSpace(Query)) && Results.Count == 0;

    public SearchViewModel(MainWindowViewModel main)
    {
        _main = main;
        _main.Project.Files.CollectionChanged += (_, _) => Refresh();
        Results.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowNoResults));
        };
    }

    // ---- All structured-filter props route back to Refresh() via these partials. ----
    partial void OnQueryChanged(string value) { Refresh(); OnPropertyChanged(nameof(ShowNoResults)); }
    partial void OnIncludeDefaultsChanged(bool value) => Refresh();
    partial void OnIncludeGroupsChanged(bool value) => Refresh();
    partial void OnIncludeMapsChanged(bool value) => Refresh();
    partial void OnIncludeOverridesChanged(bool value) => Refresh();
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

    /// <summary>Empties every Advanced input; keeps the main Query and category toggles.</summary>
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

        // Short-circuit: neither the main query nor any Advanced filter → nothing to do.
        if (tokens.Count == 0 && !HasAnyFilter()) return;

        Log.Debug("Search", $"Refresh tokens=[{string.Join(", ", tokens)}], advanced={HasAnyFilter()}");

        // ----- Project-level Default (singleton across all files) -----
        if (IncludeDefaults && _main.Project.DefaultOwner is { DefaultSettings: { } def } owner)
        {
            var cand = new Candidate(
                Kind: SearchResultKind.Default,
                DisplayText: new[] { "Default", owner.DisplayName },
                File: owner,
                Props: def);
            if (Matches(tokens, cand))
                Add(new SearchResult(SearchResultKind.Default, Localization.Get("Search.Kind.Default"), owner.DisplayName, owner, owner));
        }

        foreach (var file in _main.Project.Files)
        {
            if (IncludeGroups)
            {
                foreach (var g in file.Groups)
                {
                    var cand = new Candidate(
                        Kind: SearchResultKind.Group,
                        DisplayText: new[] { g.GroupName },
                        File: file,
                        Props: g.Properties,
                        GroupName: g.GroupName);
                    if (Matches(tokens, cand))
                        Add(new SearchResult(SearchResultKind.Group, g.GroupName, file.DisplayName, file, g));

                    if (IncludeOverrides)
                    {
                        foreach (var ov in g.DaySettings)
                        {
                            var ovCand = new Candidate(
                                Kind: SearchResultKind.Override,
                                DisplayText: new[] { g.GroupName, ov.Name },
                                File: file,
                                Props: ov.Properties,
                                GroupName: g.GroupName,
                                OverrideName: ov.Name);
                            if (Matches(tokens, ovCand))
                                Add(new SearchResult(SearchResultKind.Override, $"{g.GroupName} / {ov.Name}", file.DisplayName, file, ov));
                        }
                    }
                }
            }

            if (IncludeMaps)
            {
                foreach (var m in file.Maps)
                {
                    var cand = new Candidate(
                        Kind: SearchResultKind.Map,
                        DisplayText: new[] { m.MapName, m.Properties.MapNameAlias },
                        File: file,
                        Props: m.Properties,
                        MapName: m.MapName);
                    if (Matches(tokens, cand))
                        Add(new SearchResult(SearchResultKind.Map, m.MapName, file.DisplayName, file, m));

                    if (IncludeOverrides)
                    {
                        foreach (var ov in m.DaySettings)
                        {
                            var ovCand = new Candidate(
                                Kind: SearchResultKind.Override,
                                DisplayText: new[] { m.MapName, ov.Name },
                                File: file,
                                Props: ov.Properties,
                                MapName: m.MapName,
                                OverrideName: ov.Name);
                            if (Matches(tokens, ovCand))
                                Add(new SearchResult(SearchResultKind.Override, $"{m.MapName} / {ov.Name}", file.DisplayName, file, ov));
                        }
                    }
                }
            }

            if (Results.Count >= MaxResults) break;
        }
    }

    private void Add(SearchResult r)
    {
        if (Results.Count < MaxResults) Results.Add(r);
    }

    // ========== Matching ==========

    private bool Matches(IReadOnlyList<string> tokens, Candidate c)
    {
        // Plain tokens: every one must appear somewhere in the display text.
        foreach (var tok in tokens)
        {
            var hit = false;
            foreach (var h in c.DisplayText)
                if (!string.IsNullOrEmpty(h) && h.Contains(tok, StringComparison.OrdinalIgnoreCase))
                { hit = true; break; }
            if (!hit) return false;
        }

        // --- Text filters (Advanced) ---
        if (!string.IsNullOrWhiteSpace(GroupFilter) && !MatchGroup(c, GroupFilter)) return false;
        if (!string.IsNullOrWhiteSpace(AliasFilter) && !Contains(c.Props?.MapNameAlias, AliasFilter)) return false;
        if (!string.IsNullOrWhiteSpace(DescriptionFilter) && !Contains(c.Props?.MapDescription, DescriptionFilter)) return false;
        if (!string.IsNullOrWhiteSpace(FileNameFilter) && !MatchFile(c, FileNameFilter)) return false;

        // --- Numeric filters ---
        if (!string.IsNullOrWhiteSpace(WorkshopIdFilter))
        {
            var target = SearchNumericOps.TryParseLong(WorkshopIdFilter);
            if (target is null) return false;
            if (c.Props?.HasWorkshopId != true || c.Props!.WorkshopId != target) return false;
        }
        if (!Numeric(c.Props, CooldownOp, CooldownValue, c.Props?.HasCooldown, c.Props?.Cooldown)) return false;
        if (!Numeric(c.Props, MaxPlayersOp, MaxPlayersValue, c.Props?.HasMaxPlayers, c.Props?.MaxPlayers)) return false;
        if (!Numeric(c.Props, MinPlayersOp, MinPlayersValue, c.Props?.HasMinPlayers, c.Props?.MinPlayers)) return false;
        if (!Numeric(c.Props, MapTimeOp, MapTimeValue, c.Props?.HasMapTime, c.Props?.MapTime)) return false;
        if (!Numeric(c.Props, MapRoundsOp, MapRoundsValue, c.Props?.HasMapRounds, c.Props?.MapRounds)) return false;

        // --- Tri-state flags ---
        if (!Tri(DisabledFilter, c.Props?.HasIsDisabled == true ? c.Props!.IsDisabled : (bool?)null)) return false;
        if (!Tri(OnlyNominationFilter, c.Props?.HasOnlyNomination == true ? c.Props!.OnlyNomination : (bool?)null)) return false;
        if (!Tri(ProhibitAdminFilter, c.Props?.HasProhibitAdminNomination == true ? c.Props!.ProhibitAdminNomination : (bool?)null)) return false;

        // --- Day / Extras ---
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

    /// <summary>Group filter matches the group's own name AND any map/override that
    /// inherits from a group whose name contains the filter text.</summary>
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

    /// <summary>
    /// Numeric filter: op=Any short-circuits to "no filter". Otherwise require the value
    /// to parse and the property to be set; unset properties never match once a numeric
    /// filter is active because the server defaults fill those in elsewhere.
    /// </summary>
    private static bool Numeric(PropertySet? _, NumericOp op, string text, bool? hasFlag, int? value)
    {
        if (op == NumericOp.Any) return true;
        var target = SearchNumericOps.TryParseLong(text);
        if (target is null) return true; // user typed op but no value → don't hide everything
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

    /// <summary>
    /// All the info the matcher needs for one candidate. Fields left null simply never match
    /// the corresponding filter (e.g. overrides have no extras, so <see cref="Props"/>
    /// pointing at the override's PropertySet is enough — extras still work if set on it).
    /// </summary>
    private sealed record Candidate(
        SearchResultKind Kind,
        string[] DisplayText,
        MapConfigFile File,
        PropertySet? Props,
        string? GroupName = null,
        string? MapName = null,
        string? OverrideName = null);

    [RelayCommand]
    private void Open(SearchResult? result)
    {
        // Intentionally leaves the search window open — users often jump between several
        // results in one session, so auto-closing forces them to re-open and retype.
        // Escape / the window's X still close it.
        if (result is null) return;
        _main.NavigateToSearchResult(result);
    }

    /// <summary>Enter key on the query box: open the first match if there is one.</summary>
    [RelayCommand]
    private void OpenTop()
    {
        if (Results.Count > 0) Open(Results[0]);
    }

    /// <summary>Escape — closes the search window via the owner.</summary>
    [RelayCommand]
    private void Close() => _main.CloseSearchWindow();
}

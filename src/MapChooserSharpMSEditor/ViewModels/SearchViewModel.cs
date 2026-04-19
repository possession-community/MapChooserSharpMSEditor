using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels;

/// <summary>
/// Quick-find for the sidebar. Scans all loaded files and surfaces matching
/// Defaults / Groups / Maps / Overrides. Query is whitespace-tokenized and every
/// token must appear somewhere in the candidate's searchable text (AND semantics).
/// </summary>
public sealed partial class SearchViewModel : ViewModelBase
{
    private const int MaxResults = 50;

    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _includeDefaults = true;
    [ObservableProperty] private bool _includeGroups = true;
    [ObservableProperty] private bool _includeMaps = true;
    [ObservableProperty] private bool _includeOverrides = true;

    public ObservableCollection<SearchResult> Results { get; } = new();

    public bool HasResults => Results.Count > 0;
    public bool ShowNoResults => !string.IsNullOrWhiteSpace(Query) && Results.Count == 0;

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

    partial void OnQueryChanged(string value) { Refresh(); OnPropertyChanged(nameof(ShowNoResults)); }
    partial void OnIncludeDefaultsChanged(bool value) => Refresh();
    partial void OnIncludeGroupsChanged(bool value) => Refresh();
    partial void OnIncludeMapsChanged(bool value) => Refresh();
    partial void OnIncludeOverridesChanged(bool value) => Refresh();

    private void Refresh()
    {
        Results.Clear();
        var q = Query?.Trim() ?? string.Empty;
        if (q.Length == 0) return;

        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var file in _main.Project.Files)
        {
            if (IncludeDefaults && MatchAll(tokens, "Default", file.DisplayName))
                Add(new SearchResult(SearchResultKind.Default, Localization.Get("Search.Kind.Default"), file.DisplayName, file, file));

            if (IncludeGroups)
            {
                foreach (var g in file.Groups)
                {
                    if (MatchAll(tokens, g.GroupName))
                        Add(new SearchResult(SearchResultKind.Group, g.GroupName, file.DisplayName, file, g));
                    if (IncludeOverrides)
                        foreach (var ov in g.DaySettings)
                            if (MatchAll(tokens, g.GroupName, ov.Name))
                                Add(new SearchResult(SearchResultKind.Override, $"{g.GroupName} / {ov.Name}", file.DisplayName, file, ov));
                }
            }

            if (IncludeMaps)
            {
                foreach (var m in file.Maps)
                {
                    if (MatchAll(tokens, m.MapName, m.Properties.MapNameAlias))
                        Add(new SearchResult(SearchResultKind.Map, m.MapName, file.DisplayName, file, m));
                    if (IncludeOverrides)
                        foreach (var ov in m.DaySettings)
                            if (MatchAll(tokens, m.MapName, ov.Name))
                                Add(new SearchResult(SearchResultKind.Override, $"{m.MapName} / {ov.Name}", file.DisplayName, file, ov));
                }
            }

            if (Results.Count >= MaxResults) break;
        }
    }

    private void Add(SearchResult r)
    {
        if (Results.Count < MaxResults) Results.Add(r);
    }

    /// <summary>Every token must appear in at least one of the haystacks (case-insensitive).</summary>
    private static bool MatchAll(string[] tokens, params string?[] haystacks)
    {
        foreach (var tok in tokens)
        {
            var hit = false;
            foreach (var h in haystacks)
            {
                if (!string.IsNullOrEmpty(h) && h.Contains(tok, StringComparison.OrdinalIgnoreCase))
                {
                    hit = true;
                    break;
                }
            }
            if (!hit) return false;
        }
        return true;
    }

    [RelayCommand]
    private void Open(SearchResult? result)
    {
        if (result is null) return;
        _main.NavigateToSearchResult(result);
    }

    /// <summary>
    /// Enter key on the query box: open the first match if there is one.
    /// </summary>
    [RelayCommand]
    private void OpenTop()
    {
        if (Results.Count > 0) Open(Results[0]);
    }
}

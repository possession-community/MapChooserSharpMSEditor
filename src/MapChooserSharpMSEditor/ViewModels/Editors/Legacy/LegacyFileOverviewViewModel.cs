// LEGACY — remove when MCS migration completes
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

public sealed partial class LegacyFileOverviewViewModel : ViewModelBase
{
    public LegacyMapConfigFile File { get; }
    private readonly ViewModels.MainWindowViewModel _main;

    [ObservableProperty] private string _filter = string.Empty;

    public ObservableCollection<LegacyGroupEntry> FilteredGroups { get; } = new();
    public ObservableCollection<LegacyMapEntry> FilteredMaps { get; } = new();

    [ObservableProperty] private string _groupsHeading = "";
    [ObservableProperty] private string _mapsHeading = "";

    public LegacyFileOverviewViewModel(LegacyMapConfigFile file, ViewModels.MainWindowViewModel main)
    {
        File = file;
        _main = main;
        Rebuild();
        RefreshHeadings();

        File.Groups.CollectionChanged += (_, _) => { Rebuild(); RefreshHeadings(); };
        File.Maps.CollectionChanged += (_, _) => { Rebuild(); RefreshHeadings(); };
    }

    private void RefreshHeadings()
    {
        GroupsHeading = Localization.Format("Overview.GroupsCount", File.Groups.Count);
        MapsHeading = Localization.Format("Overview.MapsCount", File.Maps.Count);
    }

    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        var f = Filter?.Trim() ?? string.Empty;

        FilteredGroups.Clear();
        foreach (var g in File.Groups)
            if (string.IsNullOrEmpty(f) || Contains(g.GroupName, f))
                FilteredGroups.Add(g);

        FilteredMaps.Clear();
        foreach (var m in File.Maps)
            if (string.IsNullOrEmpty(f) || Contains(m.MapName, f) || Contains(m.Properties.MapNameAlias, f))
                FilteredMaps.Add(m);
    }

    private static bool Contains(string? text, string needle) =>
        !string.IsNullOrEmpty(text) && text.Contains(needle, StringComparison.OrdinalIgnoreCase);

    [RelayCommand] private void OpenMap(LegacyMapEntry? m) { if (m is not null) _main.LegacyNavigateToMap(m); }
    [RelayCommand] private void OpenGroup(LegacyGroupEntry? g) { if (g is not null) _main.LegacyNavigateToGroup(g); }
    [RelayCommand] private void OpenDefault() => _main.LegacyNavigateToDefault();
}

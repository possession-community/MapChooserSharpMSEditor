using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

/// <summary>
/// Landing view for a selected .toml file: shows every group and map it defines, with
/// click-to-navigate. Shares a single filter input across both lists so the user can
/// type "ze_" and see matches in either category.
/// </summary>
public sealed partial class FileOverviewViewModel : ViewModelBase
{
    public MapConfigFile File { get; }
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _filter = string.Empty;

    public ObservableCollection<GroupEntryModel> FilteredGroups { get; } = new();
    public ObservableCollection<MapEntryModel> FilteredMaps { get; } = new();

    public FileOverviewViewModel(MapConfigFile file, MainWindowViewModel main)
    {
        File = file;
        _main = main;
        Rebuild();

        // Live refresh if the file's groups/maps change (add/rename/remove).
        File.Groups.CollectionChanged += (_, _) => Rebuild();
        File.Maps.CollectionChanged += (_, _) => Rebuild();
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

    [RelayCommand] private void OpenMap(MapEntryModel? m) { if (m is not null) _main.NavigateToMap(m); }
    [RelayCommand] private void OpenGroup(GroupEntryModel? g) { if (g is not null) _main.NavigateToGroup(g); }
    [RelayCommand] private void OpenDefault() => _main.NavigateToDefault(File);
}

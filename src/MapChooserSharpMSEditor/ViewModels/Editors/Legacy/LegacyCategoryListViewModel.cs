// LEGACY — remove when MCS migration completes
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.ViewModels.TreeNodes;

namespace MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

public sealed partial class LegacyCategoryListViewModel : ViewModelBase
{
    public LegacyMapConfigFile File { get; }
    public CategoryKind Kind { get; }
    public string Heading => Localization.Get(Kind == CategoryKind.Maps ? "Category.Maps" : "Category.Groups");
    public string EmptyText => Localization.Get(Kind == CategoryKind.Maps ? "Category.EmptyMaps" : "Category.EmptyGroups");

    private readonly ViewModels.MainWindowViewModel _main;

    [ObservableProperty] private string _filter = string.Empty;

    public ObservableCollection<object> Entries { get; } = new();

    public LegacyCategoryListViewModel(LegacyMapConfigFile file, CategoryKind kind, ViewModels.MainWindowViewModel main)
    {
        File = file;
        Kind = kind;
        _main = main;
        Rebuild();
        if (kind == CategoryKind.Maps)
            file.Maps.CollectionChanged += (_, _) => Rebuild();
        else
            file.Groups.CollectionChanged += (_, _) => Rebuild();
    }

    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        var f = Filter?.Trim() ?? string.Empty;
        Entries.Clear();

        if (Kind == CategoryKind.Maps)
        {
            foreach (var m in File.Maps)
                if (string.IsNullOrEmpty(f) || Match(m.MapName, f) || Match(m.Properties.MapNameAlias, f))
                    Entries.Add(m);
        }
        else
        {
            foreach (var g in File.Groups)
                if (string.IsNullOrEmpty(f) || Match(g.GroupName, f))
                    Entries.Add(g);
        }
    }

    private static bool Match(string? text, string needle) =>
        !string.IsNullOrEmpty(text) && text.Contains(needle, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void Open(object? item)
    {
        switch (item)
        {
            case LegacyMapEntry m: _main.LegacyNavigateToMap(m); break;
            case LegacyGroupEntry g: _main.LegacyNavigateToGroup(g); break;
        }
    }

    [RelayCommand]
    private void AddEntry()
    {
        if (Kind == CategoryKind.Maps)
        {
            var name = UniqueName("new_map", n => { foreach (var m in File.Maps) if (m.MapName == n) return true; return false; });
            var map = new LegacyMapEntry { MapName = name };
            File.Maps.Add(map);
            _main.LegacyNavigateToMap(map);
        }
        else
        {
            var name = UniqueName("NewGroup", n => { foreach (var g in File.Groups) if (g.GroupName == n) return true; return false; });
            var group = new LegacyGroupEntry { GroupName = name };
            File.Groups.Add(group);
            _main.LegacyNavigateToGroup(group);
        }
    }

    private static string UniqueName(string baseName, Func<string, bool> exists)
    {
        var i = 0;
        while (true)
        {
            var candidate = i == 0 ? baseName : $"{baseName}{i}";
            if (!exists(candidate)) return candidate;
            i++;
        }
    }
}

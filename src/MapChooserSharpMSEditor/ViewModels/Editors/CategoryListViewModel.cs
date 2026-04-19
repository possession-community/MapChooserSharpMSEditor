using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.ViewModels.TreeNodes;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

/// <summary>
/// Searchable list of every map or group in a single file. Used when the user selects the
/// Maps or Groups category node in the tree.
/// </summary>
public sealed partial class CategoryListViewModel : ViewModelBase
{
    public MapConfigFile File { get; }
    public CategoryKind Kind { get; }
    public string Heading => Localization.Get(Kind == CategoryKind.Maps ? "Category.Maps" : "Category.Groups");
    public string EmptyText => Localization.Get(Kind == CategoryKind.Maps ? "Category.EmptyMaps" : "Category.EmptyGroups");

    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _filter = string.Empty;

    /// <summary>Entries after filtering — populated with <see cref="MapEntryModel"/> or <see cref="GroupEntryModel"/>.</summary>
    public ObservableCollection<object> Entries { get; } = new();

    public CategoryListViewModel(MapConfigFile file, CategoryKind kind, MainWindowViewModel main)
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
            case MapEntryModel m: _main.NavigateToMap(m); break;
            case GroupEntryModel g: _main.NavigateToGroup(g); break;
        }
    }

    [RelayCommand]
    private void AddEntry()
    {
        if (Kind == CategoryKind.Maps)
        {
            var name = UniqueName("new_map", n => { foreach (var m in File.Maps) if (m.MapName == n) return true; return false; });
            var map = new MapEntryModel { MapName = name };
            File.Maps.Add(map);
            _main.NavigateToMap(map);
        }
        else
        {
            var name = UniqueName("NewGroup", n => { foreach (var g in File.Groups) if (g.GroupName == n) return true; return false; });
            var group = new GroupEntryModel { GroupName = name };
            File.Groups.Add(group);
            _main.NavigateToGroup(group);
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

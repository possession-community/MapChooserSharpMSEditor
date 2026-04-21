using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Models;

/// <summary>One section path (e.g. <c>de_mirage</c>) that exists in two or more loaded
/// files. Used by the workspace warning banner.</summary>
public sealed record SectionCollision(string Path, IReadOnlyList<MapConfigFile> Owners);

/// <summary>
/// Session-wide context: tracks every loaded <see cref="MapConfigFile"/> so that cross-file
/// concerns (e.g. group-name autocomplete, "unresolved group reference" warnings) can be
/// driven from a single source of truth.
/// </summary>
public sealed class ProjectContext : ObservableObject
{
    public ObservableCollection<MapConfigFile> Files { get; } = new();

    /// <summary>All unique group names across every open file, kept live.</summary>
    public ObservableCollection<string> AllGroupNames { get; } = new();

    /// <summary>Shared undo/redo history across the whole session.</summary>
    public UndoManager Undo { get; } = new();

    /// <summary>
    /// The single file that physically owns the <c>[MapChooserSharpSettings.Default]</c>
    /// section on disk. The server rejects multiple Defaults at parse time, so for a valid
    /// project this is at most one file. Use <see cref="DefaultOwners"/> to see the full
    /// list when you need to surface a "multiple Defaults found" warning in the UI.
    /// </summary>
    public MapConfigFile? DefaultOwner => DefaultOwners.FirstOrDefault();

    /// <summary>Every file that currently carries a non-null <see cref="MapConfigFile.DefaultSettings"/>.</summary>
    public IReadOnlyList<MapConfigFile> DefaultOwners =>
        Files.Where(f => f.DefaultSettings is not null).ToList();

    public bool HasMultipleDefaults => DefaultOwners.Count > 1;

    /// <summary>
    /// Lists every section path (e.g. <c>MapChooserSharpSettings.Groups.Foo</c>,
    /// <c>de_mirage</c>, <c>de_mirage.DaySettings.Night</c>) that appears in more than
    /// one loaded file. The server's TOML parser would reject the concatenated document,
    /// so any entry here means the config won't load.
    /// </summary>
    public IReadOnlyList<SectionCollision> SectionCollisions
    {
        get
        {
            var byPath = new Dictionary<string, List<MapConfigFile>>(StringComparer.Ordinal);
            foreach (var f in Files)
            {
                foreach (var path in EnumerateSectionPaths(f))
                {
                    if (!byPath.TryGetValue(path, out var list))
                        byPath[path] = list = new List<MapConfigFile>();
                    list.Add(f);
                }
            }
            var collisions = new List<SectionCollision>();
            foreach (var (path, owners) in byPath)
                if (owners.Count > 1)
                    collisions.Add(new SectionCollision(path, owners));
            collisions.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return collisions;
        }
    }

    public bool HasSectionCollisions => SectionCollisions.Count > 0;

    private static IEnumerable<string> EnumerateSectionPaths(MapConfigFile f)
    {
        if (f.DefaultSettings is not null)
            yield return "MapChooserSharpSettings.Default";

        foreach (var g in f.Groups)
        {
            if (string.IsNullOrEmpty(g.GroupName)) continue;
            yield return $"MapChooserSharpSettings.Groups.{g.GroupName}";
            foreach (var ov in g.DaySettings)
                if (!string.IsNullOrEmpty(ov.Name))
                    yield return $"MapChooserSharpSettings.Groups.{g.GroupName}.DaySettings.{ov.Name}";
        }
        foreach (var m in f.Maps)
        {
            if (string.IsNullOrEmpty(m.MapName)) continue;
            yield return m.MapName;
            foreach (var ov in m.DaySettings)
                if (!string.IsNullOrEmpty(ov.Name))
                    yield return $"{m.MapName}.DaySettings.{ov.Name}";
        }
    }

    public ProjectContext()
    {
        Files.CollectionChanged += OnFilesChanged;
    }

    public void Add(MapConfigFile file)
    {
        if (Files.Contains(file)) return;
        Files.Add(file);
        Log.Debug("Project", $"Attached {file.DisplayName} (total={Files.Count})");
    }

    public void Remove(MapConfigFile file)
    {
        if (Files.Remove(file))
            Log.Debug("Project", $"Detached {file.DisplayName} (total={Files.Count})");
    }

    public bool IsKnownGroup(string name) =>
        !string.IsNullOrEmpty(name) && AllGroupNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    private void OnFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (MapConfigFile f in e.NewItems)
                Attach(f);
        }
        if (e.OldItems is not null)
        {
            foreach (MapConfigFile f in e.OldItems)
                Detach(f);
        }
        Rebuild();
        // Files coming/going can change which file (if any) owns the Default.
        OnPropertyChanged(nameof(DefaultOwner));
        OnPropertyChanged(nameof(DefaultOwners));
        OnPropertyChanged(nameof(HasMultipleDefaults));
        NotifyStructureChanged();
    }

    private void Attach(MapConfigFile f)
    {
        f.Groups.CollectionChanged += OnGroupsChanged;
        foreach (var g in f.Groups) HookGroup(g);
        f.Maps.CollectionChanged += OnMapsChanged;
        foreach (var m in f.Maps) HookMap(m);
        f.PropertyChanged += OnFilePropChanged;
    }

    private void Detach(MapConfigFile f)
    {
        f.Groups.CollectionChanged -= OnGroupsChanged;
        foreach (var g in f.Groups) UnhookGroup(g);
        f.Maps.CollectionChanged -= OnMapsChanged;
        foreach (var m in f.Maps) UnhookMap(m);
        f.PropertyChanged -= OnFilePropChanged;
    }

    private void HookGroup(GroupEntryModel g)
    {
        g.PropertyChanged += OnGroupPropChanged;
        g.DaySettings.CollectionChanged += OnOverridesChanged;
        foreach (var ov in g.DaySettings) ov.PropertyChanged += OnOverridePropChanged;
    }
    private void UnhookGroup(GroupEntryModel g)
    {
        g.PropertyChanged -= OnGroupPropChanged;
        g.DaySettings.CollectionChanged -= OnOverridesChanged;
        foreach (var ov in g.DaySettings) ov.PropertyChanged -= OnOverridePropChanged;
    }
    private void HookMap(MapEntryModel m)
    {
        m.PropertyChanged += OnMapPropChanged;
        m.DaySettings.CollectionChanged += OnOverridesChanged;
        foreach (var ov in m.DaySettings) ov.PropertyChanged += OnOverridePropChanged;
    }
    private void UnhookMap(MapEntryModel m)
    {
        m.PropertyChanged -= OnMapPropChanged;
        m.DaySettings.CollectionChanged -= OnOverridesChanged;
        foreach (var ov in m.DaySettings) ov.PropertyChanged -= OnOverridePropChanged;
    }

    private void OnMapsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (MapEntryModel m in e.NewItems) HookMap(m);
        if (e.OldItems is not null)
            foreach (MapEntryModel m in e.OldItems) UnhookMap(m);
        NotifyStructureChanged();
    }

    private void OnMapPropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapEntryModel.MapName)) NotifyStructureChanged();
    }

    private void OnOverridesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (DaySettingsOverrideModel ov in e.NewItems) ov.PropertyChanged += OnOverridePropChanged;
        if (e.OldItems is not null)
            foreach (DaySettingsOverrideModel ov in e.OldItems) ov.PropertyChanged -= OnOverridePropChanged;
        NotifyStructureChanged();
    }

    private void OnOverridePropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DaySettingsOverrideModel.Name)) NotifyStructureChanged();
    }

    private void NotifyStructureChanged()
    {
        OnPropertyChanged(nameof(SectionCollisions));
        OnPropertyChanged(nameof(HasSectionCollisions));
    }

    /// <summary>Republish the derived Default-owner properties whenever a file swaps its
    /// DefaultSettings reference, so any editor/tree node bound to the project-level
    /// getters refreshes automatically.</summary>
    private void OnFilePropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MapConfigFile.DefaultSettings)) return;
        OnPropertyChanged(nameof(DefaultOwner));
        OnPropertyChanged(nameof(DefaultOwners));
        OnPropertyChanged(nameof(HasMultipleDefaults));
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (GroupEntryModel g in e.NewItems) HookGroup(g);
        if (e.OldItems is not null)
            foreach (GroupEntryModel g in e.OldItems) UnhookGroup(g);
        Rebuild();
        NotifyStructureChanged();
    }

    private void OnGroupPropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupEntryModel.GroupName))
        {
            Rebuild();
            NotifyStructureChanged();
        }
    }

    private void Rebuild()
    {
        var unique = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Files)
            foreach (var g in file.Groups)
                if (!string.IsNullOrWhiteSpace(g.GroupName))
                    unique.Add(g.GroupName);

        // Apply diff in-place so bindings survive.
        var current = new HashSet<string>(AllGroupNames, StringComparer.OrdinalIgnoreCase);
        foreach (var name in unique)
            if (!current.Contains(name))
                AllGroupNames.Add(name);
        for (var i = AllGroupNames.Count - 1; i >= 0; i--)
            if (!unique.Contains(AllGroupNames[i]))
                AllGroupNames.RemoveAt(i);
    }
}

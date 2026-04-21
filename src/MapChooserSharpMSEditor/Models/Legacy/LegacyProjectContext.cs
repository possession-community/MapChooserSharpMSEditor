// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Models.Legacy;

public sealed record LegacySectionCollision(string Path, IReadOnlyList<LegacyMapConfigFile> Owners);

public sealed class LegacyProjectContext : ObservableObject
{
    public ObservableCollection<LegacyMapConfigFile> Files { get; } = new();
    public ObservableCollection<string> AllGroupNames { get; } = new();
    public UndoManager Undo { get; } = new();

    public LegacyMapConfigFile? DefaultOwner => DefaultOwners.FirstOrDefault();

    public IReadOnlyList<LegacyMapConfigFile> DefaultOwners =>
        Files.Where(f => f.DefaultSettings is not null).ToList();

    public bool HasMultipleDefaults => DefaultOwners.Count > 1;

    public IReadOnlyList<LegacySectionCollision> SectionCollisions
    {
        get
        {
            var byPath = new Dictionary<string, List<LegacyMapConfigFile>>(StringComparer.Ordinal);
            foreach (var f in Files)
            {
                foreach (var path in EnumerateSectionPaths(f))
                {
                    if (!byPath.TryGetValue(path, out var list))
                        byPath[path] = list = new List<LegacyMapConfigFile>();
                    list.Add(f);
                }
            }
            var collisions = new List<LegacySectionCollision>();
            foreach (var (path, owners) in byPath)
                if (owners.Count > 1)
                    collisions.Add(new LegacySectionCollision(path, owners));
            collisions.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return collisions;
        }
    }

    public bool HasSectionCollisions => SectionCollisions.Count > 0;

    private static IEnumerable<string> EnumerateSectionPaths(LegacyMapConfigFile f)
    {
        if (f.DefaultSettings is not null)
            yield return "MapChooserSharpSettings.Default";

        foreach (var g in f.Groups)
        {
            if (string.IsNullOrEmpty(g.GroupName)) continue;
            yield return $"MapChooserSharpSettings.Groups.{g.GroupName}";
        }
        foreach (var m in f.Maps)
        {
            if (string.IsNullOrEmpty(m.MapName)) continue;
            yield return m.MapName;
        }
    }

    public LegacyProjectContext()
    {
        Files.CollectionChanged += OnFilesChanged;
    }

    public void Add(LegacyMapConfigFile file)
    {
        if (Files.Contains(file)) return;
        Files.Add(file);
        Log.Debug("LegacyProject", $"Attached {file.DisplayName} (total={Files.Count})");
    }

    public void Remove(LegacyMapConfigFile file)
    {
        if (Files.Remove(file))
            Log.Debug("LegacyProject", $"Detached {file.DisplayName} (total={Files.Count})");
    }

    public bool IsKnownGroup(string name) =>
        !string.IsNullOrEmpty(name) && AllGroupNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    private void OnFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (LegacyMapConfigFile f in e.NewItems) Attach(f);
        if (e.OldItems is not null)
            foreach (LegacyMapConfigFile f in e.OldItems) Detach(f);
        Rebuild();
        OnPropertyChanged(nameof(DefaultOwner));
        OnPropertyChanged(nameof(DefaultOwners));
        OnPropertyChanged(nameof(HasMultipleDefaults));
        NotifyStructureChanged();
    }

    private void Attach(LegacyMapConfigFile f)
    {
        f.Groups.CollectionChanged += OnGroupsChanged;
        foreach (var g in f.Groups) HookGroup(g);
        f.Maps.CollectionChanged += OnMapsChanged;
        foreach (var m in f.Maps) HookMap(m);
        f.PropertyChanged += OnFilePropChanged;
    }

    private void Detach(LegacyMapConfigFile f)
    {
        f.Groups.CollectionChanged -= OnGroupsChanged;
        foreach (var g in f.Groups) UnhookGroup(g);
        f.Maps.CollectionChanged -= OnMapsChanged;
        foreach (var m in f.Maps) UnhookMap(m);
        f.PropertyChanged -= OnFilePropChanged;
    }

    private void HookGroup(LegacyGroupEntry g) => g.PropertyChanged += OnGroupPropChanged;
    private void UnhookGroup(LegacyGroupEntry g) => g.PropertyChanged -= OnGroupPropChanged;
    private void HookMap(LegacyMapEntry m) => m.PropertyChanged += OnMapPropChanged;
    private void UnhookMap(LegacyMapEntry m) => m.PropertyChanged -= OnMapPropChanged;

    private void OnMapsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (LegacyMapEntry m in e.NewItems) HookMap(m);
        if (e.OldItems is not null)
            foreach (LegacyMapEntry m in e.OldItems) UnhookMap(m);
        NotifyStructureChanged();
    }

    private void OnMapPropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LegacyMapEntry.MapName)) NotifyStructureChanged();
    }

    private void NotifyStructureChanged()
    {
        OnPropertyChanged(nameof(SectionCollisions));
        OnPropertyChanged(nameof(HasSectionCollisions));
    }

    private void OnFilePropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LegacyMapConfigFile.DefaultSettings)) return;
        OnPropertyChanged(nameof(DefaultOwner));
        OnPropertyChanged(nameof(DefaultOwners));
        OnPropertyChanged(nameof(HasMultipleDefaults));
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (LegacyGroupEntry g in e.NewItems) HookGroup(g);
        if (e.OldItems is not null)
            foreach (LegacyGroupEntry g in e.OldItems) UnhookGroup(g);
        Rebuild();
        NotifyStructureChanged();
    }

    private void OnGroupPropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LegacyGroupEntry.GroupName))
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

        var current = new HashSet<string>(AllGroupNames, StringComparer.OrdinalIgnoreCase);
        foreach (var name in unique)
            if (!current.Contains(name))
                AllGroupNames.Add(name);
        for (var i = AllGroupNames.Count - 1; i >= 0; i--)
            if (!unique.Contains(AllGroupNames[i]))
                AllGroupNames.RemoveAt(i);
    }
}

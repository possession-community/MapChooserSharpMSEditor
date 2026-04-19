using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Models;

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

    public ProjectContext()
    {
        Files.CollectionChanged += OnFilesChanged;
    }

    public void Add(MapConfigFile file)
    {
        if (!Files.Contains(file))
            Files.Add(file);
    }

    public void Remove(MapConfigFile file) => Files.Remove(file);

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
    }

    private void Attach(MapConfigFile f)
    {
        f.Groups.CollectionChanged += OnGroupsChanged;
        foreach (var g in f.Groups)
            g.PropertyChanged += OnGroupPropChanged;
    }

    private void Detach(MapConfigFile f)
    {
        f.Groups.CollectionChanged -= OnGroupsChanged;
        foreach (var g in f.Groups)
            g.PropertyChanged -= OnGroupPropChanged;
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (GroupEntryModel g in e.NewItems) g.PropertyChanged += OnGroupPropChanged;
        if (e.OldItems is not null)
            foreach (GroupEntryModel g in e.OldItems) g.PropertyChanged -= OnGroupPropChanged;
        Rebuild();
    }

    private void OnGroupPropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupEntryModel.GroupName))
            Rebuild();
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

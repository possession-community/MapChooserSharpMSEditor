using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.ViewModels.Editors;
using MapChooserSharpMSEditor.ViewModels.TreeNodes;
using MapChooserSharpMSEditor.Views;

namespace MapChooserSharpMSEditor.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<TreeNodeBase> Tree { get; } = new();

    /// <summary>Shared across every editor for cross-file concerns (group autocomplete, etc.).</summary>
    public ProjectContext Project { get; } = new();

    public ResolvedPropertiesViewModel Resolved { get; } = new();

    /// <summary>Undo history (owned by Project so every VM that receives Project has access).</summary>
    public UndoManager Undo => Project.Undo;

    public SearchViewModel Search { get; }

    [ObservableProperty] private TreeNodeBase? _selectedNode;
    [ObservableProperty] private ViewModelBase _currentEditor;
    [ObservableProperty] private string _statusText = Localization.Get("Status.Ready");

    /// <summary>
    /// Toggle for the right-hand "Resolved Values" panel. Collapsible so the editor area
    /// can reclaim the horizontal space when not needed.
    /// </summary>
    [ObservableProperty] private bool _isResolvedPanelVisible = true;

    public MainWindowViewModel()
    {
        _currentEditor = new WelcomeViewModel();
        Search = new SearchViewModel(this);
        Undo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UndoManager.CanUndo))
                UndoActionCommand.NotifyCanExecuteChanged();
            else if (e.PropertyName == nameof(UndoManager.CanRedo))
                RedoActionCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand]
    private void ToggleResolvedPanel() => IsResolvedPanelVisible = !IsResolvedPanelVisible;

    /// <summary>Available languages for the View → Language submenu.</summary>
    public LocaleOption[] AvailableLocales => Localization.AvailableLocales;

    /// <summary>Current locale (Id). Used by menu items to show a checkmark for the active choice.</summary>
    public string CurrentLocale => Localization.CurrentLocale;

    [RelayCommand]
    private async Task SelectLanguageAsync(string? localeId)
    {
        if (string.IsNullOrEmpty(localeId)) return;
        if (localeId == Localization.CurrentLocale) return;

        UserSettings.SetLocale(localeId);

        var top = GetTopLevel() as Window;
        if (top is null) return;

        var title = Localization.Get("Restart.Title");
        var message = Localization.Get("Restart.Message");
        var yes = Localization.Get("Restart.Yes");
        var no = Localization.Get("Restart.No");
        var restart = await ConfirmDialog.ShowAsync(top, title, message, yes, no);

        if (restart) RestartApp();
    }

    private static void RestartApp()
    {
        // Environment.ProcessPath gives the host exe for a published app; when `dotnet run`
        // launched us it still points at the dotnet host which re-launches the same process.
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            try { System.Diagnostics.Process.Start(exe); } catch { /* best effort */ }
        }
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    [RelayCommand(CanExecute = nameof(CanUndoExec))]
    private void UndoAction() => Undo.Undo();

    [RelayCommand(CanExecute = nameof(CanRedoExec))]
    private void RedoAction() => Undo.Redo();

    private bool CanUndoExec() => Undo.CanUndo;
    private bool CanRedoExec() => Undo.CanRedo;

    /// <summary>
    /// Programmatic tree navigation: used by overview/list views to jump to a specific map or
    /// group via a button click. Walks the whole tree so folder hierarchies are supported,
    /// and auto-expands every ancestor so the selection is actually visible.
    /// </summary>
    public void NavigateToMap(MapEntryModel map) =>
        SelectAndExpand(n => n is MapNode mn && mn.Map == map);

    public void NavigateToGroup(GroupEntryModel group) =>
        SelectAndExpand(n => n is GroupNode gn && gn.Group == group);

    public void NavigateToDefault(MapConfigFile file) =>
        SelectAndExpand(n => n is DefaultSettingsNode dn && dn.File == file);

    public void NavigateToOverride(DaySettingsOverrideModel ov) =>
        SelectAndExpand(n => n is OverrideNode on && on.Override == ov);

    public void NavigateToSearchResult(SearchResult r)
    {
        switch (r.Kind)
        {
            case SearchResultKind.Default: NavigateToDefault(r.File); break;
            case SearchResultKind.Group when r.Target is GroupEntryModel g: NavigateToGroup(g); break;
            case SearchResultKind.Map when r.Target is MapEntryModel m: NavigateToMap(m); break;
            case SearchResultKind.Override when r.Target is DaySettingsOverrideModel ov: NavigateToOverride(ov); break;
        }
    }

    /// <summary>
    /// Finds the first node matching <paramref name="match"/>, then expands every ancestor
    /// so the selection is scrolled into view rather than hidden behind a collapsed folder.
    /// </summary>
    private void SelectAndExpand(Func<TreeNodeBase, bool> match)
    {
        var path = FindPath(Tree, match);
        if (path is null) return;
        for (var i = 0; i < path.Count - 1; i++) path[i].IsExpanded = true;
        SelectedNode = path[^1];
    }

    private static List<TreeNodeBase>? FindPath(IEnumerable<TreeNodeBase> roots, Func<TreeNodeBase, bool> match)
    {
        foreach (var n in roots)
        {
            if (match(n)) return new List<TreeNodeBase> { n };
            var sub = FindPath(n.Children, match);
            if (sub is not null)
            {
                sub.Insert(0, n);
                return sub;
            }
        }
        return null;
    }

    private static IEnumerable<TreeNodeBase> EnumerateAllNodes(IEnumerable<TreeNodeBase> roots)
    {
        foreach (var n in roots)
        {
            yield return n;
            foreach (var child in EnumerateAllNodes(n.Children))
                yield return child;
        }
    }

    partial void OnSelectedNodeChanged(TreeNodeBase? value)
    {
        CurrentEditor = value switch
        {
            FileNode fn => new FileOverviewViewModel(fn.File, this),
            DefaultSettingsNode dn => new DefaultSettingsViewModel(dn.File, Project),
            CategoryNode cn => new CategoryListViewModel(cn.File, cn.Kind, this),
            MapNode mn => new MapEditorViewModel(mn.File, mn.Map, Project, this),
            GroupNode gn => new GroupEditorViewModel(gn.File, gn.Group, Project, this),
            OverrideNode on => new OverrideEditorViewModel(on.File, on.Parent, on.Override, Project),
            _ => new WelcomeViewModel(),
        };
        Resolved.Refresh(CurrentEditor, Project);
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("TOML") { Patterns = new[] { "*.toml" } } },
            Title = "Open MCS map config",
        });

        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                OpenPath(path);
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var top = GetTopLevel();
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open MCS config folder",
        });
        var folder = folders.FirstOrDefault();
        if (folder is null) return;
        var folderPath = folder.TryGetLocalPath();
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

        OpenFolder(folderPath);
    }

    public void OpenFolder(string folderPath)
    {
        try
        {
            var rootNode = BuildFolderNode(folderPath, isRoot: true);
            if (rootNode.Children.Count == 0)
            {
                StatusText = Localization.Format("Status.NoTomlFound", folderPath);
                return;
            }
            Tree.Add(rootNode);
            SelectedNode = rootNode.Children.FirstOrDefault() ?? (TreeNodeBase)rootNode;
            StatusText = Localization.Format("Status.OpenedFolder", folderPath);
        }
        catch (Exception ex)
        {
            StatusText = Localization.Format("Status.LoadFailed", folderPath, ex.Message);
        }
    }

    /// <summary>
    /// Mirrors the on-disk directory layout: subfolders first (sorted), then *.toml files (sorted).
    /// Folders with no .toml anywhere in their subtree are pruned so the user sees only relevant paths.
    /// </summary>
    private FolderNode BuildFolderNode(string path, bool isRoot)
    {
        var node = new FolderNode(path, isRoot);

        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var child = BuildFolderNode(dir, isRoot: false);
            if (child.Children.Count > 0)
                node.Children.Add(child);
        }

        foreach (var f in Directory.GetFiles(path, "*.toml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var file = TomlConfigLoader.LoadFile(f);
                AttachDirtyTracking(file);
                Project.Add(file);
                node.Children.Add(BuildFileNode(file));
            }
            catch (Exception ex)
            {
                StatusText = Localization.Format("Status.Skipped", f, ex.Message);
            }
        }

        return node;
    }

    public void OpenPath(string path)
    {
        try
        {
            var file = TomlConfigLoader.LoadFile(path);
            Project.Add(file);
            AttachDirtyTracking(file);
            Undo.Clear();
            var node = BuildFileNode(file);
            Tree.Add(node);
            SelectedNode = node;
            StatusText = Localization.Format("Status.Loaded", path);
        }
        catch (Exception ex)
        {
            StatusText = Localization.Format("Status.LoadFailed", path, ex.Message);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedNode is null) return;
        var file = GetFileForNode(SelectedNode);
        if (file is null) return;
        SaveFile(file);
    }

    [RelayCommand]
    private void SaveAll()
    {
        foreach (var fn in EnumerateFileNodes(Tree))
            SaveFile(fn.File);
    }

    private static IEnumerable<FileNode> EnumerateFileNodes(IEnumerable<TreeNodeBase> nodes)
    {
        foreach (var n in nodes)
        {
            if (n is FileNode fn)
                yield return fn;
            else
                foreach (var sub in EnumerateFileNodes(n.Children))
                    yield return sub;
        }
    }

    private FolderNode? FindParentFolder(FileNode target) =>
        FindParentFolderIn(Tree, target);

    private static FolderNode? FindParentFolderIn(IEnumerable<TreeNodeBase> nodes, FileNode target)
    {
        foreach (var n in nodes)
        {
            if (n is FolderNode folder)
            {
                if (folder.Children.Contains(target))
                    return folder;
                var sub = FindParentFolderIn(folder.Children, target);
                if (sub is not null) return sub;
            }
        }
        return null;
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (SelectedNode is null) return;
        var file = GetFileForNode(SelectedNode);
        if (file is null) return;

        var top = GetTopLevel();
        if (top is null) return;
        var result = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = file.DisplayName,
            DefaultExtension = "toml",
            FileTypeChoices = new[] { new FilePickerFileType("TOML") { Patterns = new[] { "*.toml" } } },
            Title = "Save as",
        });
        if (result is null) return;
        var path = result.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        file.FilePath = path;
        file.DisplayName = Path.GetFileName(path);
        SaveFile(file);
    }

    [RelayCommand]
    private void NewFile()
    {
        var file = new MapConfigFile
        {
            DisplayName = "untitled.toml",
            DefaultSettings = new PropertySet(),
        };
        AttachDirtyTracking(file);
        Project.Add(file);
        file.IsDirty = true;
        var node = BuildFileNode(file);
        Tree.Add(node);
        SelectedNode = node;
    }

    [RelayCommand]
    private void CloseFile()
    {
        if (SelectedNode is null) return;
        var file = GetFileForNode(SelectedNode);
        if (file is null) return;

        var fileNode = EnumerateFileNodes(Tree).FirstOrDefault(n => n.File == file);
        if (fileNode is null) return;

        Project.Remove(file);
        Undo.Clear();
        var parent = FindParentFolder(fileNode);
        if (parent is not null)
        {
            parent.Children.Remove(fileNode);
            // Unwind now-empty folders up to the root.
            while (parent is not null && parent.Children.Count == 0)
            {
                var grand = FindParentFolderOfFolder(parent);
                if (grand is null)
                {
                    Tree.Remove(parent);
                    parent = null;
                }
                else
                {
                    grand.Children.Remove(parent);
                    parent = grand;
                }
            }
        }
        else
        {
            Tree.Remove(fileNode);
        }

        if (Tree.Count == 0)
        {
            SelectedNode = null;
            CurrentEditor = new WelcomeViewModel();
        }
    }

    private FolderNode? FindParentFolderOfFolder(FolderNode target) =>
        FindParentFolderOfFolderIn(Tree, target);

    private static FolderNode? FindParentFolderOfFolderIn(IEnumerable<TreeNodeBase> nodes, FolderNode target)
    {
        foreach (var n in nodes)
        {
            if (n is FolderNode folder)
            {
                if (folder.Children.Contains(target)) return folder;
                var sub = FindParentFolderOfFolderIn(folder.Children, target);
                if (sub is not null) return sub;
            }
        }
        return null;
    }

    [RelayCommand]
    private void AddMap()
    {
        if (SelectedNode is null) return;
        var file = GetFileForNode(SelectedNode);
        if (file is null) return;

        var name = UniqueName("new_map", n => file.Maps.Any(m => m.MapName == n));
        var map = new MapEntryModel { MapName = name };
        file.Maps.Add(map);
    }

    [RelayCommand]
    private void AddGroup()
    {
        if (SelectedNode is null) return;
        var file = GetFileForNode(SelectedNode);
        if (file is null) return;

        var name = UniqueName("NewGroup", n => file.Groups.Any(g => g.GroupName == n));
        var group = new GroupEntryModel { GroupName = name };
        file.Groups.Add(group);
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

    private static MapConfigFile? GetFileForNode(TreeNodeBase node) => node switch
    {
        FileNode f => f.File,
        DefaultSettingsNode d => d.File,
        CategoryNode c => c.File,
        MapNode m => m.File,
        GroupNode g => g.File,
        OverrideNode o => o.File,
        FolderNode => null,
        _ => null,
    };

    private void SaveFile(MapConfigFile file)
    {
        try
        {
            if (string.IsNullOrEmpty(file.FilePath))
            {
                // Trigger Save-As instead.
                _ = SaveAsAsync();
                return;
            }
            TomlConfigWriter.SaveFile(file);
            StatusText = Localization.Format("Status.Saved", file.FilePath);
        }
        catch (Exception ex)
        {
            StatusText = Localization.Format("Status.SaveFailed", ex.Message);
        }
    }

    private FileNode BuildFileNode(MapConfigFile file)
    {
        var root = new FileNode(file);

        var defaultNode = new DefaultSettingsNode(file);
        root.Children.Add(defaultNode);

        var groupsNode = new CategoryNode(file, CategoryKind.Groups);
        foreach (var g in file.Groups)
            groupsNode.Children.Add(BuildGroupNode(file, g));
        root.Children.Add(groupsNode);

        var mapsNode = new CategoryNode(file, CategoryKind.Maps);
        foreach (var m in file.Maps)
            mapsNode.Children.Add(BuildMapNode(file, m));
        root.Children.Add(mapsNode);

        // Keep tree in sync with model collections.
        file.Groups.CollectionChanged += (_, e) =>
            HandleCollectionChange(e, groupsNode, item => BuildGroupNode(file, (GroupEntryModel)item));
        file.Maps.CollectionChanged += (_, e) =>
            HandleCollectionChange(e, mapsNode, item => BuildMapNode(file, (MapEntryModel)item));

        return root;
    }

    private MapNode BuildMapNode(MapConfigFile file, MapEntryModel map)
    {
        var node = new MapNode(file, map);
        foreach (var ov in map.DaySettings)
            node.Children.Add(new OverrideNode(file, map, ov));
        map.DaySettings.CollectionChanged += (_, e) =>
            HandleCollectionChange(e, node, item => new OverrideNode(file, map, (DaySettingsOverrideModel)item));
        return node;
    }

    private GroupNode BuildGroupNode(MapConfigFile file, GroupEntryModel group)
    {
        var node = new GroupNode(file, group);
        foreach (var ov in group.DaySettings)
            node.Children.Add(new OverrideNode(file, group, ov));
        group.DaySettings.CollectionChanged += (_, e) =>
            HandleCollectionChange(e, node, item => new OverrideNode(file, group, (DaySettingsOverrideModel)item));
        return node;
    }

    private static void HandleCollectionChange(
        NotifyCollectionChangedEventArgs e,
        TreeNodeBase parent,
        Func<object, TreeNodeBase> factory)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            parent.Children.Clear();
            return;
        }
        if (e.NewItems is not null)
        {
            var insertAt = e.NewStartingIndex >= 0 ? e.NewStartingIndex : parent.Children.Count;
            foreach (var item in e.NewItems)
                parent.Children.Insert(insertAt++, factory(item));
        }
        if (e.OldItems is not null)
        {
            // Remove by matching underlying model, not by index, since Handle is fired after the
            // source collection has already applied the change.
            foreach (var item in e.OldItems)
            {
                var match = parent.Children.FirstOrDefault(c => c switch
                {
                    MapNode mn => mn.Map == item,
                    GroupNode gn => gn.Group == item,
                    OverrideNode on => on.Override == item,
                    _ => false,
                });
                if (match is not null)
                    parent.Children.Remove(match);
            }
        }
    }

    private void AttachDirtyTracking(MapConfigFile file)
    {
        void MarkDirty()
        {
            file.IsDirty = true;
            // Any edit anywhere in the project can shift what the currently previewed
            // selection resolves to (e.g. editing the Default row changes every map's preview).
            Resolved.Refresh(CurrentEditor, Project);
        }
        file.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MapConfigFile.IsDirty)) MarkDirty();
        };

        void HookPropertySet(PropertySet? p)
        {
            if (p is null) return;
            UndoHooks.HookObservable(p, Undo);
            p.PropertyChanged += (_, _) => MarkDirty();

            UndoHooks.HookCollection(p.GroupSettings, Undo);
            p.GroupSettings.CollectionChanged += (_, _) => MarkDirty();

            UndoHooks.HookCollection(p.DaysAllowed, Undo);
            p.DaysAllowed.CollectionChanged += (_, _) => MarkDirty();

            UndoHooks.HookCollection(p.AllowedTimeRanges, Undo);
            p.AllowedTimeRanges.CollectionChanged += (_, _) => MarkDirty();

            UndoHooks.HookCollection(p.Extras, Undo);
            p.Extras.CollectionChanged += (_, e) =>
            {
                MarkDirty();
                if (e.NewItems is not null)
                    foreach (ExtraSection sec in e.NewItems) HookExtraSection(sec);
            };
            foreach (var sec in p.Extras) HookExtraSection(sec);
        }

        void HookExtraSection(ExtraSection sec)
        {
            UndoHooks.HookObservable(sec, Undo);
            UndoHooks.HookCollection(sec.Entries, Undo);
            sec.Entries.CollectionChanged += (_, e) =>
            {
                MarkDirty();
                if (e.NewItems is not null)
                    foreach (ExtraKeyValue kv in e.NewItems) UndoHooks.HookObservable(kv, Undo);
            };
            foreach (var kv in sec.Entries) UndoHooks.HookObservable(kv, Undo);
        }

        HookPropertySet(file.DefaultSettings);
        foreach (var g in file.Groups) HookGroup(g, MarkDirty, HookPropertySet);
        foreach (var m in file.Maps) HookMap(m, MarkDirty, HookPropertySet);

        UndoHooks.HookCollection(file.Groups, Undo);
        file.Groups.CollectionChanged += (_, e) =>
        {
            MarkDirty();
            if (e.NewItems is not null) foreach (GroupEntryModel g in e.NewItems) HookGroup(g, MarkDirty, HookPropertySet);
        };

        UndoHooks.HookCollection(file.Maps, Undo);
        file.Maps.CollectionChanged += (_, e) =>
        {
            MarkDirty();
            if (e.NewItems is not null) foreach (MapEntryModel m in e.NewItems) HookMap(m, MarkDirty, HookPropertySet);
        };
    }

    private void HookGroup(GroupEntryModel g, Action markDirty, Action<PropertySet?> hookProps)
    {
        UndoHooks.HookObservable(g, Undo);
        g.PropertyChanged += (_, _) => markDirty();
        hookProps(g.Properties);
        foreach (var ov in g.DaySettings) HookOverride(ov, markDirty, hookProps);
        UndoHooks.HookCollection(g.DaySettings, Undo);
        g.DaySettings.CollectionChanged += (_, e) =>
        {
            markDirty();
            if (e.NewItems is not null) foreach (DaySettingsOverrideModel ov in e.NewItems) HookOverride(ov, markDirty, hookProps);
        };
    }

    private void HookMap(MapEntryModel m, Action markDirty, Action<PropertySet?> hookProps)
    {
        UndoHooks.HookObservable(m, Undo);
        m.PropertyChanged += (_, _) => markDirty();
        hookProps(m.Properties);
        foreach (var ov in m.DaySettings) HookOverride(ov, markDirty, hookProps);
        UndoHooks.HookCollection(m.DaySettings, Undo);
        m.DaySettings.CollectionChanged += (_, e) =>
        {
            markDirty();
            if (e.NewItems is not null) foreach (DaySettingsOverrideModel ov in e.NewItems) HookOverride(ov, markDirty, hookProps);
        };
    }

    private void HookOverride(DaySettingsOverrideModel ov, Action markDirty, Action<PropertySet?> hookProps)
    {
        UndoHooks.HookObservable(ov, Undo);
        ov.PropertyChanged += (_, _) => markDirty();
        hookProps(ov.Properties);
        UndoHooks.HookCollection(ov.TargetDays, Undo);
        ov.TargetDays.CollectionChanged += (_, _) => markDirty();
        UndoHooks.HookCollection(ov.TargetTimeRanges, Undo);
        ov.TargetTimeRanges.CollectionChanged += (_, _) => markDirty();
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}

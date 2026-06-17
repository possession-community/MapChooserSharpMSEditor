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
using MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

namespace MapChooserSharpMSEditor.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Active schema mode. Toggling it swaps the workspace between Current
    /// (MapChooserSharpMS) and Legacy (MapChooserSharp.API v0.1.5) trees so the user can
    /// only have one schema open at a time. Legacy plumbing lives in the partial file
    /// MainWindowViewModel.Legacy.cs.
    /// </summary>
    [ObservableProperty] private AppMode _mode = AppMode.Current;

    public ObservableCollection<TreeNodeBase> Tree { get; } = new();

    /// <summary>Shared across every editor for cross-file concerns (group autocomplete, etc.).</summary>
    public ProjectContext Project { get; } = new();

    public ResolvedPropertiesViewModel Resolved { get; } = new();

    /// <summary>Undo history (owned by Project so every VM that receives Project has access).</summary>
    public UndoManager Undo => Mode == AppMode.Legacy ? LegacyUndo : Project.Undo;

    public SearchViewModel Search { get; }

    [ObservableProperty] private TreeNodeBase? _selectedNode;
    [ObservableProperty] private ViewModelBase _currentEditor;
    [ObservableProperty] private string _statusText = Localization.Get("Status.Ready");

    /// <summary>
    /// Toggle for the right-hand "Resolved Values" panel. Collapsible so the editor area
    /// can reclaim the horizontal space when not needed.
    /// </summary>
    [ObservableProperty] private bool _isResolvedPanelVisible = true;

    /// <summary>Debug console panel along the bottom edge. Off by default — flipped on
    /// via View → Debug Console when the user wants to see internal operation traces.</summary>
    [ObservableProperty] private bool _isDebugConsoleVisible;

    [RelayCommand]
    private void ToggleDebugConsole() => IsDebugConsoleVisible = !IsDebugConsoleVisible;

    public MainWindowViewModel()
    {
        _currentEditor = new WelcomeViewModel();
        Search = new SearchViewModel(this);
        Undo.PropertyChanged += (_, e) =>
        {
            if (Mode != AppMode.Current) return;
            if (e.PropertyName == nameof(UndoManager.CanUndo))
                UndoActionCommand.NotifyCanExecuteChanged();
            else if (e.PropertyName == nameof(UndoManager.CanRedo))
                RedoActionCommand.NotifyCanExecuteChanged();
        };
        Project.Files.CollectionChanged += (_, _) => SyncProjectDefaultNode();
        InitializeLegacyHooks();
    }

    private ProjectDefaultNode? _projectDefaultNode;

    /// <summary>
    /// Keeps exactly one <see cref="ProjectDefaultNode"/> pinned at the top of the tree
    /// whenever the project has at least one file, and removes it when the workspace
    /// empties. It deliberately sits above the file/folder roots since the Default is a
    /// project-wide concept rather than a per-file one.
    /// </summary>
    private void SyncProjectDefaultNode()
    {
        var shouldHave = Project.Files.Count > 0;
        if (shouldHave && _projectDefaultNode is null)
        {
            _projectDefaultNode = new ProjectDefaultNode();
            Tree.Insert(0, _projectDefaultNode);
        }
        else if (!shouldHave && _projectDefaultNode is not null)
        {
            Tree.Remove(_projectDefaultNode);
            _projectDefaultNode = null;
        }
    }

    [RelayCommand]
    private void ToggleResolvedPanel() => IsResolvedPanelVisible = !IsResolvedPanelVisible;

    private SearchWindow? _searchWindow;

    [RelayCommand(CanExecute = nameof(CanOpenSearch))]
    private void OpenSearch()
    {
        Log.Debug("Search", "OpenSearch window");
        if (_searchWindow is { IsVisible: true })
        {
            _searchWindow.Activate();
            return;
        }

        _searchWindow = new SearchWindow { DataContext = Search };
        _searchWindow.Closed += (_, _) => _searchWindow = null;

        if (GetTopLevel() is Window owner)
            _searchWindow.Show(owner);
        else
            _searchWindow.Show();
    }

    public void CloseSearchWindow() => _searchWindow?.Close();

    /// <summary>
    /// Opens the Workshop-check dialog so the user can bulk-query every loaded map's
    /// WorkshopId against Steam and pick which private/deleted items to mark Disabled.
    /// Result count is surfaced via StatusText after the dialog closes.
    /// </summary>
    private bool CanOpenSearch() => Mode == AppMode.Current;
    private bool CanOpenWorkshop() => Mode == AppMode.Current;
    private bool CanOpenBatchEdit() => Mode == AppMode.Current;

    [RelayCommand(CanExecute = nameof(CanOpenWorkshop))]
    private async Task OpenWorkshopCheckAsync()
    {
        Log.Debug("Workshop", "OpenWorkshopCheck window");
        if (GetTopLevel() is not Window owner) return;
        var vm = new WorkshopCheckViewModel(Project);
        var dlg = new WorkshopCheckWindow { DataContext = vm };
        await dlg.ShowDialog(owner);
        if (dlg.Tag is int applied && applied > 0)
        {
            Log.Info("Workshop", $"Applied Disabled=true to {applied} map(s)");
            StatusText = Localization.Format("WorkshopCheck.Applied", applied);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenBatchEdit))]
    private async Task OpenBatchEditAsync()
    {
        Log.Debug("BatchEdit", "OpenBatchEdit window");
        if (GetTopLevel() is not Window owner) return;
        var vm = new BatchEditViewModel(Project);
        var dlg = new BatchEditWindow { DataContext = vm };
        vm.Owner = dlg;
        await dlg.ShowDialog(owner);
        // Surface the last status from whichever tab the user used.
        var msg = !string.IsNullOrEmpty(vm.GroupStatusText) ? vm.GroupStatusText
                : !string.IsNullOrEmpty(vm.PropertyStatusText) ? vm.PropertyStatusText
                : null;
        if (msg is not null) StatusText = msg;
    }

    [RelayCommand]
    private async Task OpenMigrationAsync()
    {
        Log.Debug("Migration", "OpenMigration window");
        if (GetTopLevel() is not Window owner) return;
        var vm = new MigrationViewModel { Owner = owner };
        var dlg = new MigrationWindow { DataContext = vm };
        vm.Owner = dlg;
        await dlg.ShowDialog(owner);
    }

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

    /// <summary>Internal helpers exposed to the Legacy partial.</summary>
    internal static TopLevel? GetTopLevelInternal() => GetTopLevel();
    internal void _projectDefaultNodeInternalReset() => _projectDefaultNode = null;
    internal void SelectAndExpandInternal(System.Func<TreeNodeBase, bool> match) => SelectAndExpand(match);
    internal void ResetIfEmptyInternal() => ResetIfEmpty();
    internal static string UniqueNameInternal(string baseName, System.Func<string, bool> exists) => UniqueName(baseName, exists);
    internal FolderNode? FindParentFolderOfFolderInPublic(FolderNode target) => FindParentFolderOfFolder(target);
    internal FolderNode? FindOrCreateFolderForInternal(string filePath) => FindOrCreateFolderFor(filePath);

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
        // There's only one Default node for the whole project now; the file argument is
        // ignored (search results still carry a file reference, but the node itself is
        // project-scoped). Kept for API compatibility with NavigateToSearchResult.
        SelectAndExpand(n => n is ProjectDefaultNode);

    public void NavigateToOverride(DaySettingsOverrideModel ov) =>
        SelectAndExpand(n => n is OverrideNode on && on.Override == ov);

    public void NavigateToSearchResult(SearchResult r)
    {
        Log.Debug("Navigate", $"Search result → {r.Kind}: {r.Label} ({r.FileName})");
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

    // Browser-style navigation history for SelectedNode. Mouse XButton1 / XButton2
    // (and Alt+Left / Alt+Right) pop entries; any normal selection change pushes the
    // previous node onto the back stack and invalidates the forward stack.
    private readonly Stack<TreeNodeBase> _backStack = new();
    private readonly Stack<TreeNodeBase> _forwardStack = new();
    private bool _isNavigatingHistory;
    private TreeNodeBase? _previousSelectedNode;

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private void NavigateBack()
    {
        while (_backStack.Count > 0)
        {
            var target = _backStack.Pop();
            if (!IsNodeInTree(target)) continue; // skip entries whose file got closed
            if (SelectedNode is not null) _forwardStack.Push(SelectedNode);
            _isNavigatingHistory = true;
            try { SelectedNode = target; } finally { _isNavigatingHistory = false; }
            NotifyHistoryCommands();
            return;
        }
        NotifyHistoryCommands();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private void NavigateForward()
    {
        while (_forwardStack.Count > 0)
        {
            var target = _forwardStack.Pop();
            if (!IsNodeInTree(target)) continue;
            if (SelectedNode is not null) _backStack.Push(SelectedNode);
            _isNavigatingHistory = true;
            try { SelectedNode = target; } finally { _isNavigatingHistory = false; }
            NotifyHistoryCommands();
            return;
        }
        NotifyHistoryCommands();
    }

    private bool CanNavigateBack() => _backStack.Count > 0;
    private bool CanNavigateForward() => _forwardStack.Count > 0;

    private void NotifyHistoryCommands()
    {
        NavigateBackCommand.NotifyCanExecuteChanged();
        NavigateForwardCommand.NotifyCanExecuteChanged();
    }

    internal void ClearNavigationHistory()
    {
        _backStack.Clear();
        _forwardStack.Clear();
        _previousSelectedNode = null;
        NotifyHistoryCommands();
    }

    private bool IsNodeInTree(TreeNodeBase target)
    {
        foreach (var n in EnumerateAllNodes(Tree))
            if (ReferenceEquals(n, target)) return true;
        return false;
    }

    partial void OnSelectedNodeChanged(TreeNodeBase? value)
    {
        // Normal selection change: remember the previous node so back/forward have something
        // to pop. History navigation replays selections, which must NOT feed the stacks or
        // they'd grow unboundedly.
        if (!_isNavigatingHistory)
        {
            if (_previousSelectedNode is not null && _previousSelectedNode != value)
            {
                _backStack.Push(_previousSelectedNode);
                _forwardStack.Clear(); // branching off old forward history invalidates it
                NotifyHistoryCommands();
            }
        }
        _previousSelectedNode = value;

        CurrentEditor = value switch
        {
            FileNode fn => new FileOverviewViewModel(fn.File, this),
            ProjectDefaultNode => new DefaultSettingsViewModel(Project),
            CategoryNode cn => new CategoryListViewModel(cn.File, cn.Kind, this),
            MapNode mn => new MapEditorViewModel(mn.File, mn.Map, Project, this),
            GroupNode gn => new GroupEditorViewModel(gn.File, gn.Group, Project, this),
            OverrideNode on => new OverrideEditorViewModel(on.File, on.Parent, on.Override, Project),
            // Legacy nodes
            LegacyFileNode lfn => new LegacyFileOverviewViewModel(lfn.File, this),
            LegacyProjectDefaultNode => new LegacyDefaultSettingsViewModel(LegacyProject),
            LegacyCategoryNode lcn => new LegacyCategoryListViewModel(lcn.File, lcn.Kind, this),
            LegacyMapNode lmn => new LegacyMapEditorViewModel(lmn.File, lmn.Map, LegacyProject),
            LegacyGroupNode lgn => new LegacyGroupEditorViewModel(lgn.File, lgn.Group, LegacyProject),
            _ => new WelcomeViewModel(),
        };
        if (Mode == AppMode.Current)
        {
            Resolved.Refresh(CurrentEditor, Project);
            LegacyResolved.Clear();
        }
        else
        {
            LegacyResolved.Refresh(CurrentEditor, LegacyProject);
            Resolved.Clear();
        }
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        Log.Debug("File", "OpenFile picker invoked");
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
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

            // Schema sniff before we touch the loader: if the file belongs to the other
            // mode we offer an explicit switch so the user doesn't silently drop keys
            // (e.g. NominationCost parsed by Legacy but written back in Current mode
            // would vanish on save).
            if (!await EnsureMatchingModeAsync(ConfigSchemaDetector.DetectFromFile(path), forFolder: false)) continue;

            Log.Info("File", $"Opening {path}");
            OpenPath(path);
        }
    }

    /// <summary>
    /// Compare <paramref name="detected"/> to the current <see cref="Mode"/>. Returns true
    /// if the open should proceed (either the schemas match, the sniff was inconclusive,
    /// or the user picked "open anyway"). A false return means the user cancelled.
    /// When the user picks "switch mode", this method actually does the switch before
    /// returning true.
    /// </summary>
    private async Task<bool> EnsureMatchingModeAsync(ConfigSchemaKind detected, bool forFolder)
    {
        if (detected == ConfigSchemaKind.Ambiguous) return true;
        var wantMode = detected == ConfigSchemaKind.Legacy ? AppMode.Legacy : AppMode.Current;
        if (wantMode == Mode) return true;
        if (GetTopLevel() is not Window owner) return true;

        var messageKey = (wantMode, forFolder) switch
        {
            (AppMode.Legacy, false) => "SchemaMismatch.ToLegacy",
            (AppMode.Current, false) => "SchemaMismatch.ToCurrent",
            (AppMode.Legacy, true) => "SchemaMismatch.FolderToLegacy",
            _ => "SchemaMismatch.FolderToCurrent",
        };

        var choice = await ConfirmDialog.ShowTriAsync(
            owner,
            Localization.Get("SchemaMismatch.Title"),
            Localization.Get(messageKey),
            Localization.Get("SchemaMismatch.Switch"),
            Localization.Get("SchemaMismatch.OpenAnyway"),
            Localization.Get("SchemaMismatch.Cancel"));

        switch (choice)
        {
            case ConfirmDialog.TriResult.Primary:
                await SwitchModeAsync(wantMode.ToString());
                // SwitchModeAsync bails out if the user refuses to drop dirty files;
                // re-check so we don't proceed with the open in the wrong mode.
                return Mode == wantMode;
            case ConfirmDialog.TriResult.Secondary:
                return true;
            default:
                return false;
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        // Folder picker is mode-agnostic — we always pop it here, sniff the folder's
        // schema, then dispatch to the right loader (and offer to switch modes first
        // if the folder looks like it belongs to the other schema).
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

        if (!await EnsureMatchingModeAsync(ConfigSchemaDetector.DetectFromFolder(folderPath), forFolder: true)) return;

        if (Mode == AppMode.Legacy)
        {
            // Re-enter the Legacy picker? No — we already have folderPath, so go straight
            // to its load path, skipping LegacyDispatchOpenFolderAsync's own picker.
            await LegacyOpenFolderAfterPickAsync(folderPath);
            return;
        }

        // Enforce single-root-folder policy: opening a second folder while one is already
        // loaded made per-file origin tracking ambiguous (nested roots, duplicate files,
        // edits silently landing in only one of the two trees). Close the previous root
        // before loading the new one so the workspace always has exactly one loaded folder.
        var existingRoot = Tree.OfType<FolderNode>().FirstOrDefault(n => n.IsRoot);
        if (existingRoot is not null || _pinnedMapsTomlNode is not null)
        {
            var filesInside = new List<FileNode>();
            if (existingRoot is not null)
                filesInside.AddRange(EnumerateFileNodes(new[] { (TreeNodeBase)existingRoot }));
            // The pinned maps.toml lives at the top of Tree, so it isn't picked up by the
            // folder-tree walk above. Add it explicitly so the discard-confirm sees it too.
            if (_pinnedMapsTomlNode is not null)
                filesInside.Add(_pinnedMapsTomlNode);

            if (!await ConfirmDiscardIfDirtyAsync(filesInside)) return;
            foreach (var f in filesInside) RemoveFileNodeFromTree(f);
            if (existingRoot is not null) DetachFolderIfStillAttached(existingRoot);
            _pinnedMapsTomlNode = null;
        }

        OpenFolder(folderPath);
    }

    /// <summary>
    /// If the folder's root contains <c>maps.toml</c>, the server uses that file exclusively
    /// and ignores the rest. We mirror that distinction visually: the maps.toml file is
    /// pinned as its own top-level Explorer entry (separate from the folder tree) so it's
    /// obvious which file is "the" config, while sub-folder .toml files remain in the
    /// directory-mirrored tree for reference / migration.
    /// </summary>
    private FileNode? _pinnedMapsTomlNode;

    public void OpenFolder(string folderPath)
    {
        if (Mode == AppMode.Legacy) { LegacyOpenFolder(folderPath); return; }
        Log.Info("File", $"OpenFolder({folderPath})");
        try
        {
            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            FileNode? mapsNode = null;

            var mapsTomlPath = Path.Combine(folderPath, "maps.toml");
            if (File.Exists(mapsTomlPath))
            {
                Log.Debug("File", "Root maps.toml detected — pinning as top-level entry");
                try
                {
                    var file = TomlConfigLoader.LoadFile(mapsTomlPath);
                    AttachDirtyTracking(file);
                    Project.Add(file);
                    mapsNode = BuildFileNode(file);
                    skip.Add(mapsTomlPath);
                }
                catch (Exception ex)
                {
                    StatusText = Localization.Format("Status.Skipped", mapsTomlPath, ex.Message);
                }
            }

            var rootNode = BuildFolderNode(folderPath, isRoot: true, skip);

            if (mapsNode is null && rootNode.Children.Count == 0)
            {
                StatusText = Localization.Format("Status.NoTomlFound", folderPath);
                return;
            }

            // Pin maps.toml above the folder tree so it's the first thing the user sees.
            if (mapsNode is not null)
            {
                Tree.Add(mapsNode);
                _pinnedMapsTomlNode = mapsNode;
            }
            if (rootNode.Children.Count > 0) Tree.Add(rootNode);

            SelectedNode = mapsNode ?? rootNode.Children.FirstOrDefault() ?? (TreeNodeBase)rootNode;
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
    /// <paramref name="skipPaths"/> is used to exclude files that should be presented elsewhere
    /// (maps.toml gets pulled out by OpenFolder so it doesn't appear twice).
    /// </summary>
    private FolderNode BuildFolderNode(string path, bool isRoot, HashSet<string>? skipPaths = null)
    {
        var node = new FolderNode(path, isRoot);

        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var child = BuildFolderNode(dir, isRoot: false, skipPaths);
            if (child.Children.Count > 0)
                node.Children.Add(child);
        }

        foreach (var f in Directory.GetFiles(path, "*.toml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (skipPaths is not null && skipPaths.Contains(f)) continue;
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
        if (Mode == AppMode.Legacy) { LegacyOpenPath(path); return; }
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
        if (Mode == AppMode.Legacy) { LegacyDispatchSave(); return; }
        if (SelectedNode is null) return;
        var file = GetFileForNode(SelectedNode);
        if (file is null) return;
        Log.Debug("File", $"Save invoked for {file.DisplayName}");
        SaveFile(file);
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        if (Mode == AppMode.Legacy) { await LegacyDispatchSaveAllAsync(); return; }
        // Skip clean files: rewriting them strips comments and reorders properties even
        // though the model didn't change, so SaveAll on a 100-file workspace would touch
        // every file on disk for no semantic reason.
        var dirtyFiles = EnumerateFileNodes(Tree)
            .Select(n => n.File)
            .Where(f => f.IsDirty)
            .ToList();

        if (dirtyFiles.Count == 0)
        {
            StatusText = Localization.Get("Status.SaveAllNoDirty");
            return;
        }

        // Bulk confirmation: list every file that's about to be written so the user
        // can spot a surprise (e.g. a background tool marked something dirty) before
        // the serializer touches disk.
        if (GetTopLevel() is Window owner)
        {
            var names = dirtyFiles.Select(f => f.DisplayName);
            var message = string.Format(Localization.Get("Confirm.SaveAll.Message"), string.Join("\n  • ", names));
            var ok = await ConfirmDialog.ShowAsync(
                owner,
                Localization.Get("Confirm.SaveAll.Title"), message,
                Localization.Get("Confirm.SaveAll.Yes"),
                Localization.Get("Confirm.SaveAll.No"));
            if (!ok) return;
        }

        foreach (var f in dirtyFiles) SaveFile(f);
        StatusText = Localization.Format("Status.SaveAllDone", dirtyFiles.Count);
        Log.Info("File", $"SaveAll: {dirtyFiles.Count} dirty file(s) saved");
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
        if (Mode == AppMode.Legacy) { await LegacyDispatchSaveAsAsync(); return; }
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
        Log.Info("File", "NewFile (untitled)");
        var (_, node) = CreateUntitledFile();
        SelectedNode = node;
    }

    /// <summary>
    /// Spawns an in-memory untitled file with its tree node already wired up. Returns both
    /// so callers that wanted the file for a follow-up action (e.g. Add Map into a new file
    /// from the AddEntry dialog) can use it without re-walking the tree.
    /// </summary>
    private (MapConfigFile File, TreeNodeBase Node) CreateUntitledFile()
    {
        var file = new MapConfigFile
        {
            DisplayName = "untitled.toml",
            // DefaultSettings deliberately null: the project has at most one Default section
            // across every loaded file (the server's TOML parser rejects duplicates), so we
            // only attach a PropertySet when the user explicitly promotes a file to own it.
        };
        AttachDirtyTracking(file);
        Project.Add(file);
        file.IsDirty = true;
        var node = BuildFileNode(file);
        Tree.Add(node);
        return (file, node);
    }

    /// <summary>
    /// Creates a new file destined for <paramref name="path"/> and inserts its tree node
    /// into the right spot: if the path lives under an already-loaded root folder the file
    /// appears inside the matching folder hierarchy (creating missing intermediate folder
    /// nodes as needed), otherwise it's added at the tree root like an ad-hoc open.
    /// </summary>
    private (MapConfigFile File, FileNode Node) CreateFileAtPath(string path)
    {
        Log.Info("File", $"CreateFileAtPath({path})");
        var file = new MapConfigFile
        {
            FilePath = path,
            DisplayName = Path.GetFileName(path),
            // See CreateUntitledFile: only the one "owner" file carries DefaultSettings.
        };
        AttachDirtyTracking(file);
        Project.Add(file);
        file.IsDirty = true;
        var node = BuildFileNode(file);

        var folder = FindOrCreateFolderFor(path);
        if (folder is not null)
        {
            InsertFileSorted(folder, node);
            folder.IsExpanded = true;
        }
        else
        {
            Tree.Add(node);
        }

        return (file, node);
    }

    /// <summary>
    /// Walks the loaded root folders looking for one that is an ancestor of
    /// <paramref name="filePath"/>. If found, descends into (or creates) the subfolder
    /// chain that ends at the file's directory and returns that leaf folder. Returns
    /// null when the path isn't under any loaded root — the caller should place the
    /// file at the tree root in that case.
    /// </summary>
    private FolderNode? FindOrCreateFolderFor(string filePath)
    {
        var fullFilePath = Path.GetFullPath(filePath);
        var targetDir = Path.GetDirectoryName(fullFilePath);
        if (string.IsNullOrEmpty(targetDir)) return null;

        foreach (var root in Tree.OfType<FolderNode>().Where(n => n.IsRoot))
        {
            var rootPath = Path.GetFullPath(root.FolderPath);
            if (!IsPathUnderFolder(fullFilePath, rootPath)) continue;

            if (string.Equals(rootPath, targetDir, StringComparison.OrdinalIgnoreCase))
                return root;

            var rel = Path.GetRelativePath(rootPath, targetDir);
            var segments = rel.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            var currentPath = rootPath;
            foreach (var seg in segments)
            {
                currentPath = Path.Combine(currentPath, seg);
                var child = current.Children
                    .OfType<FolderNode>()
                    .FirstOrDefault(f => string.Equals(Path.GetFullPath(f.FolderPath), currentPath, StringComparison.OrdinalIgnoreCase));
                if (child is null)
                {
                    child = new FolderNode(currentPath, isRoot: false);
                    InsertFolderSorted(current, child);
                }
                current.IsExpanded = true;
                current = child;
            }
            return current;
        }
        return null;
    }

    private static bool IsPathUnderFolder(string fullFilePath, string fullFolderPath)
    {
        var normalized = fullFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullFilePath.StartsWith(normalized, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Inserts <paramref name="newChild"/> into the folder's child list keeping
    /// "folders first (alpha), then files (alpha)" ordering used by <see cref="BuildFolderNode"/>.</summary>
    private static void InsertFolderSorted(FolderNode parent, FolderNode newChild)
    {
        var idx = 0;
        foreach (var c in parent.Children)
        {
            if (c is not FolderNode existing) break;
            if (StringComparer.OrdinalIgnoreCase.Compare(existing.FolderPath, newChild.FolderPath) > 0) break;
            idx++;
        }
        parent.Children.Insert(idx, newChild);
    }

    private static void InsertFileSorted(FolderNode parent, FileNode newFile)
    {
        var idx = 0;
        foreach (var c in parent.Children)
        {
            if (c is FolderNode) { idx++; continue; }
            if (c is FileNode existing
                && StringComparer.OrdinalIgnoreCase.Compare(existing.File.DisplayName, newFile.File.DisplayName) <= 0)
            {
                idx++;
                continue;
            }
            break;
        }
        parent.Children.Insert(idx, newFile);
    }

    /// <summary>
    /// Close every loaded file + clear the workspace, prompting once if any files are dirty.
    /// Mirrors the single-root-folder dirty prompt — names every dirty file, asks for one
    /// yes/no.
    /// </summary>
    [RelayCommand]
    private async Task CloseAllAsync()
    {
        if (Mode == AppMode.Legacy) { await LegacyDispatchCloseAllAsync(); return; }
        if (Tree.Count == 0) return;
        if (GetTopLevel() is not Window owner) return;

        var dirty = Project.Files.Where(f => f.IsDirty).Select(f => f.DisplayName).ToList();
        if (dirty.Count > 0)
        {
            var message = string.Format(Localization.Get("Confirm.CloseAll.Message"), string.Join("\n  • ", dirty));
            var ok = await ConfirmDialog.ShowAsync(
                owner,
                Localization.Get("Confirm.CloseAll.Title"), message,
                Localization.Get("Confirm.CloseAll.Yes"),
                Localization.Get("Confirm.CloseAll.No"));
            if (!ok) return;
        }

        Tree.Clear();
        _projectDefaultNode = null;
        _pinnedMapsTomlNode = null;
        for (var i = Project.Files.Count - 1; i >= 0; i--)
            Project.Remove(Project.Files[i]);
        Undo.Clear();
        SelectedNode = null;
        CurrentEditor = new WelcomeViewModel();
        StatusText = Localization.Get("Status.Ready");
    }

    [RelayCommand]
    private async Task CloseFileAsync()
    {
        if (Mode == AppMode.Legacy) { await LegacyDispatchCloseFileAsync(); return; }
        if (SelectedNode is null) return;
        var file = GetFileForNode(SelectedNode);
        if (file is null) return;

        var fileNode = EnumerateFileNodes(Tree).FirstOrDefault(n => n.File == file);
        if (fileNode is null) return;

        if (!await ConfirmDiscardIfDirtyAsync(new[] { fileNode })) return;
        RemoveFileNodeFromTree(fileNode);
        ResetIfEmpty();
    }

    /// <summary>
    /// Right-click action on any tree row: close the file / folder from the workspace
    /// without touching the disk. Files behave like "Close file"; folders close every
    /// file beneath them and drop the folder node itself.
    /// </summary>
    [RelayCommand]
    private async Task RemoveFromViewAsync(TreeNodeBase? node)
    {
        if (Mode == AppMode.Legacy) { await LegacyDispatchRemoveFromViewAsync(node); return; }
        if (node is null) return;

        switch (node)
        {
            case FileNode fn:
                if (!await ConfirmDiscardIfDirtyAsync(new[] { fn })) return;
                RemoveFileNodeFromTree(fn);
                break;

            case FolderNode fld:
                var filesInside = EnumerateFileNodes(new[] { (TreeNodeBase)fld }).ToList();
                if (!await ConfirmDiscardIfDirtyAsync(filesInside)) return;
                foreach (var f in filesInside) RemoveFileNodeFromTree(f);
                // RemoveFileNodeFromTree's empty-folder unwind only fires for folders that
                // actually held files; a folder containing only empty subfolders (or an
                // already-empty root) still needs an explicit detach.
                DetachFolderIfStillAttached(fld);
                break;

            // Other node kinds (Default / Category / Map / Group / Override) aren't
            // meaningful to "remove from view" — they're structural, not user-visible files.
            default:
                return;
        }

        ResetIfEmpty();
    }

    private void RemoveFileNodeFromTree(FileNode fileNode)
    {
        Log.Info("Tree", $"Remove {fileNode.File.DisplayName} from workspace");
        Project.Remove(fileNode.File);
        Undo.Clear();
        if (_pinnedMapsTomlNode == fileNode) _pinnedMapsTomlNode = null;
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
    }

    private void DetachFolderIfStillAttached(FolderNode fld)
    {
        if (Tree.Contains(fld)) { Tree.Remove(fld); return; }
        var parent = FindParentFolderOfFolder(fld);
        parent?.Children.Remove(fld);
    }

    private void ResetIfEmpty()
    {
        if (Tree.Count != 0) return;
        SelectedNode = null;
        CurrentEditor = new WelcomeViewModel();
    }

    /// <summary>
    /// Prompt once if any of the candidate files have unsaved changes. Returns false only
    /// if the user explicitly backs out, so clean-state paths skip the dialog entirely.
    /// </summary>
    private async Task<bool> ConfirmDiscardIfDirtyAsync(IEnumerable<FileNode> candidates)
    {
        var dirty = candidates.Where(n => n.File.IsDirty).Select(n => n.File.DisplayName).ToList();
        if (dirty.Count == 0) return true;
        if (GetTopLevel() is not Window owner) return true;

        var message = string.Format(Localization.Get("Confirm.DiscardDirty.Message"), string.Join("\n  • ", dirty));
        return await ConfirmDialog.ShowAsync(
            owner,
            Localization.Get("Confirm.DiscardDirty.Title"),
            message,
            Localization.Get("Confirm.DiscardDirty.Yes"),
            Localization.Get("Confirm.DiscardDirty.No"));
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
    private Task AddMapAsync()
    {
        if (Mode == AppMode.Legacy) return LegacyDispatchAddMapAsync();
        return AddEntryAsync(AddEntryKind.Map);
    }

    [RelayCommand]
    private Task AddGroupAsync()
    {
        if (Mode == AppMode.Legacy) return LegacyDispatchAddGroupAsync();
        return AddEntryAsync(AddEntryKind.Group);
    }

    /// <summary>
    /// Shared entry point for "Add Map" and "Add Group". Pops the dialog, which lets the
    /// user pick between the current file, any other loaded file, or spawning a fresh
    /// untitled file, then appends the new entry to whichever target they chose and
    /// navigates to it.
    /// </summary>
    private async Task AddEntryAsync(AddEntryKind kind)
    {
        if (GetTopLevel() is not Window owner) return;

        var current = SelectedNode is not null ? GetFileForNode(SelectedNode) : null;
        // Fall back to the first loaded file when the selection doesn't own one (e.g. FolderNode).
        current ??= Project.Files.FirstOrDefault();

        var defaultName = kind == AddEntryKind.Map
            ? UniqueName("new_map", n => Project.Files.Any(f => f.Maps.Any(m => m.MapName == n)))
            : UniqueName("NewGroup", n => Project.Files.Any(f => f.Groups.Any(g => g.GroupName == n)));

        var result = await AddEntryDialog.ShowAsync(
            owner,
            kind,
            Project.Files.ToList(),
            current,
            defaultName,
            async () =>
            {
                // Ask for the save path up front — picking at creation time avoids silently
                // writing "untitled.toml" to some directory on first save, and lets the user
                // place the new config next to their other TOMLs.
                var picked = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Localization.Get("AddEntry.NewFile.PickerTitle"),
                    SuggestedFileName = "new.toml",
                    DefaultExtension = "toml",
                    FileTypeChoices = new[] { new FilePickerFileType("TOML") { Patterns = new[] { "*.toml" } } },
                });
                if (picked is null) return null;
                var path = picked.TryGetLocalPath();
                if (string.IsNullOrEmpty(path)) return null;

                var (file, _) = CreateFileAtPath(path);
                return file;
            });

        if (result is null) return;

        if (kind == AddEntryKind.Map)
        {
            var map = new MapEntryModel { MapName = result.Name };
            result.Target.Maps.Add(map);
            Log.Info("Tree", $"Added Map '{result.Name}' → {result.Target.DisplayName}");
            NavigateToMap(map);
        }
        else
        {
            var group = new GroupEntryModel { GroupName = result.Name };
            result.Target.Groups.Add(group);
            Log.Info("Tree", $"Added Group '{result.Name}' → {result.Target.DisplayName}");
            NavigateToGroup(group);
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

    private static MapConfigFile? GetFileForNode(TreeNodeBase node) => node switch
    {
        FileNode f => f.File,
        CategoryNode c => c.File,
        MapNode m => m.File,
        GroupNode g => g.File,
        OverrideNode o => o.File,
        FolderNode => null,
        ProjectDefaultNode => null,
        _ => null,
    };

    private void SaveFile(MapConfigFile file)
    {
        try
        {
            if (string.IsNullOrEmpty(file.FilePath))
            {
                Log.Debug("File", $"Save: no path yet for {file.DisplayName}, falling back to Save-As");
                _ = SaveAsAsync();
                return;
            }
            TomlConfigWriter.SaveFile(file);
            Log.Info("File", $"Saved {file.FilePath}");
            StatusText = Localization.Format("Status.Saved", file.FilePath);
        }
        catch (Exception ex)
        {
            Log.Error("File", $"Save failed for {file.DisplayName}: {ex.Message}");
            StatusText = Localization.Format("Status.SaveFailed", ex.Message);
        }
    }

    private FileNode BuildFileNode(MapConfigFile file)
    {
        var root = new FileNode(file);

        // No per-file Default node — the project has exactly one Default, exposed via the
        // ProjectDefaultNode at the top of Tree (see EnsureProjectDefaultNode).
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

            UndoHooks.HookCollection(p.SearchTags, Undo);
            p.SearchTags.CollectionChanged += (_, _) => MarkDirty();

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

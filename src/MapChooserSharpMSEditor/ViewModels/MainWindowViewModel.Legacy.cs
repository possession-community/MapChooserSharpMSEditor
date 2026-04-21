// LEGACY — remove when MCS migration completes
//
// All Legacy-mode plumbing for MainWindowViewModel: dual-project storage, mode switching,
// Legacy file open/save/close, Legacy tree builders, and Legacy dirty tracking. Lives in
// its own partial file so deleting Legacy support boils down to:
//   1. Delete this file
//   2. Delete Models/Legacy, Services/Legacy, ViewModels/.../Legacy, Views/Legacy
//   3. Strip the Mode menu + DataTemplates from MainWindow.axaml
//   4. Drop the if (Mode == AppMode.Legacy) branches from MainWindowViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.Services.Legacy;
using MapChooserSharpMSEditor.ViewModels.Editors.Legacy;
using MapChooserSharpMSEditor.ViewModels.Legacy;
using MapChooserSharpMSEditor.ViewModels.TreeNodes;
using MapChooserSharpMSEditor.Views;
using MapChooserSharpMSEditor.Views.Legacy;

namespace MapChooserSharpMSEditor.ViewModels;

public enum AppMode { Current, Legacy }

public sealed partial class MainWindowViewModel
{
    public LegacyProjectContext LegacyProject { get; } = new();
    public UndoManager LegacyUndo => LegacyProject.Undo;
    public LegacyResolvedPropertiesViewModel LegacyResolved { get; } = new();
    public LegacySearchViewModel LegacySearch { get; private set; } = null!;

    private LegacyProjectDefaultNode? _legacyProjectDefaultNode;

    /// <summary>True while in Legacy schema mode. Exposed so XAML can hide Current-only UI.</summary>
    public bool IsLegacyMode => Mode == AppMode.Legacy;
    public bool IsCurrentMode => Mode == AppMode.Current;

    partial void OnModeChanged(AppMode value)
    {
        OnPropertyChanged(nameof(IsLegacyMode));
        OnPropertyChanged(nameof(IsCurrentMode));
        OnPropertyChanged(nameof(Undo));
        OpenSearchCommand.NotifyCanExecuteChanged();
        OpenWorkshopCheckCommand.NotifyCanExecuteChanged();
        OpenLegacySearchCommand.NotifyCanExecuteChanged();
        OpenLegacyWorkshopCheckCommand.NotifyCanExecuteChanged();
        UndoActionCommand.NotifyCanExecuteChanged();
        RedoActionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the project-Default tree node for the currently active mode. Mirrors the
    /// Current-mode SyncProjectDefaultNode but inserts the Legacy node when in Legacy mode.
    /// </summary>
    private void SyncLegacyProjectDefaultNode()
    {
        var shouldHave = LegacyProject.Files.Count > 0 && Mode == AppMode.Legacy;
        if (shouldHave && _legacyProjectDefaultNode is null)
        {
            _legacyProjectDefaultNode = new LegacyProjectDefaultNode();
            Tree.Insert(0, _legacyProjectDefaultNode);
        }
        else if (!shouldHave && _legacyProjectDefaultNode is not null)
        {
            Tree.Remove(_legacyProjectDefaultNode);
            _legacyProjectDefaultNode = null;
        }
    }

    /// <summary>Wire the Legacy hooks once at construction.</summary>
    private void InitializeLegacyHooks()
    {
        LegacySearch = new LegacySearchViewModel(this);
        LegacyProject.Files.CollectionChanged += (_, _) => SyncLegacyProjectDefaultNode();
        LegacyUndo.PropertyChanged += (_, e) =>
        {
            // The same Undo/Redo commands serve both modes; flip enabled state when the
            // active stack changes.
            if (Mode != AppMode.Legacy) return;
            if (e.PropertyName == nameof(UndoManager.CanUndo)) UndoActionCommand.NotifyCanExecuteChanged();
            else if (e.PropertyName == nameof(UndoManager.CanRedo)) RedoActionCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand]
    private async Task SwitchModeAsync(string? targetText)
    {
        if (!Enum.TryParse<AppMode>(targetText, ignoreCase: true, out var target)) return;
        if (target == Mode) return;

        // Confirm before clobbering loaded workspace.
        if (Tree.Count > 0)
        {
            var dirtyNames = CollectDirtyNames();
            if (dirtyNames.Count > 0)
            {
                if (GetTopLevelInternal() is not Window owner) return;
                var msg = string.Format(Localization.Get("Confirm.SwitchMode.Message"), string.Join("\n  • ", dirtyNames));
                var ok = await ConfirmDialog.ShowAsync(owner,
                    Localization.Get("Confirm.SwitchMode.Title"), msg,
                    Localization.Get("Confirm.SwitchMode.Yes"),
                    Localization.Get("Confirm.SwitchMode.No"));
                if (!ok) return;
            }
        }

        // Tear down whatever's loaded in the current mode.
        ClearActiveWorkspace();
        Mode = target;
        // Rebuild project-default node in case the new mode already had files (defensive — we
        // just cleared everything, but covers future paths that swap modes without clearing).
        SyncProjectDefaultNode();
        SyncLegacyProjectDefaultNode();

        StatusText = Localization.Format("Mode.Switched",
            Localization.Get(target == AppMode.Legacy ? "Mode.Legacy" : "Mode.Current"));
        Log.Info("Mode", $"Switched to {target}");
    }

    private List<string> CollectDirtyNames()
    {
        var names = new List<string>();
        if (Mode == AppMode.Current)
        {
            foreach (var f in Project.Files)
                if (f.IsDirty) names.Add(f.DisplayName);
        }
        else
        {
            foreach (var f in LegacyProject.Files)
                if (f.IsDirty) names.Add(f.DisplayName);
        }
        return names;
    }

    private void ClearActiveWorkspace()
    {
        Tree.Clear();
        _projectDefaultNodeInternalReset();
        _legacyProjectDefaultNode = null;
        _legacyPinnedMapsTomlNode = null;
        if (Mode == AppMode.Current)
        {
            for (var i = Project.Files.Count - 1; i >= 0; i--)
                Project.Remove(Project.Files[i]);
            Project.Undo.Clear();
        }
        else
        {
            for (var i = LegacyProject.Files.Count - 1; i >= 0; i--)
                LegacyProject.Remove(LegacyProject.Files[i]);
            LegacyProject.Undo.Clear();
        }
        SelectedNode = null;
        CurrentEditor = new MapChooserSharpMSEditor.ViewModels.Editors.WelcomeViewModel();
        StatusText = Localization.Get("Status.Ready");
    }

    // ===== Legacy file IO =====

    public void LegacyOpenPath(string path)
    {
        try
        {
            var file = LegacyConfigLoader.LoadFile(path);
            LegacyProject.Add(file);
            LegacyAttachDirtyTracking(file);
            LegacyUndo.Clear();
            var node = BuildLegacyFileNode(file);
            Tree.Add(node);
            SelectedNode = node;
            StatusText = Localization.Format("Status.Loaded", path);
        }
        catch (Exception ex)
        {
            StatusText = Localization.Format("Status.LoadFailed", path, ex.Message);
        }
    }

    private LegacyFileNode? _legacyPinnedMapsTomlNode;

    /// <summary>Legacy-mode OpenFolder picker entry point. Mirrors the Current dispatch:
    /// enforce single-root, prompt to discard dirty files, then load the new folder.</summary>
    internal async Task LegacyDispatchOpenFolderAsync()
    {
        var top = GetTopLevelInternal();
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Open MCS config folder (Legacy)",
        });
        var folder = folders.FirstOrDefault();
        if (folder is null) return;
        var folderPath = folder.TryGetLocalPath();
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

        var existingRoot = Tree.OfType<FolderNode>().FirstOrDefault(n => n.IsRoot);
        if (existingRoot is not null || _legacyPinnedMapsTomlNode is not null)
        {
            var filesInside = new List<LegacyFileNode>();
            if (existingRoot is not null)
                filesInside.AddRange(EnumerateLegacyFileNodes(new[] { (TreeNodeBase)existingRoot }));
            if (_legacyPinnedMapsTomlNode is not null)
                filesInside.Add(_legacyPinnedMapsTomlNode);

            if (!await ConfirmLegacyDiscardIfDirtyAsync(filesInside)) return;
            foreach (var f in filesInside) RemoveLegacyFileNodeFromTree(f);
            if (existingRoot is not null && Tree.Contains(existingRoot)) Tree.Remove(existingRoot);
            _legacyPinnedMapsTomlNode = null;
        }

        LegacyOpenFolder(folderPath);
    }

    public void LegacyOpenFolder(string folderPath)
    {
        Log.Info("LegacyFile", $"OpenFolder({folderPath})");
        try
        {
            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LegacyFileNode? mapsNode = null;

            // maps.toml pinning: mirror the Current behavior — if root contains maps.toml,
            // pin it as its own top-level entry so it's visually distinct from sub-folder
            // .toml files. The MCS server treats root-level maps.toml as the canonical
            // config when present.
            var mapsTomlPath = Path.Combine(folderPath, "maps.toml");
            if (File.Exists(mapsTomlPath))
            {
                Log.Debug("LegacyFile", "Root maps.toml detected — pinning as top-level entry");
                try
                {
                    var file = LegacyConfigLoader.LoadFile(mapsTomlPath);
                    LegacyAttachDirtyTracking(file);
                    LegacyProject.Add(file);
                    mapsNode = BuildLegacyFileNode(file);
                    skip.Add(mapsTomlPath);
                }
                catch (Exception ex)
                {
                    StatusText = Localization.Format("Status.Skipped", mapsTomlPath, ex.Message);
                }
            }

            var rootNode = BuildLegacyFolderNode(folderPath, isRoot: true, skip);

            if (mapsNode is null && rootNode.Children.Count == 0)
            {
                StatusText = Localization.Format("Status.NoTomlFound", folderPath);
                return;
            }

            if (mapsNode is not null)
            {
                Tree.Add(mapsNode);
                _legacyPinnedMapsTomlNode = mapsNode;
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

    private FolderNode BuildLegacyFolderNode(string path, bool isRoot, HashSet<string>? skipPaths = null)
    {
        var node = new FolderNode(path, isRoot);
        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var child = BuildLegacyFolderNode(dir, isRoot: false, skipPaths);
            if (child.Children.Count > 0)
                node.Children.Add(child);
        }
        foreach (var f in Directory.GetFiles(path, "*.toml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (skipPaths is not null && skipPaths.Contains(f)) continue;
            try
            {
                var file = LegacyConfigLoader.LoadFile(f);
                LegacyAttachDirtyTracking(file);
                LegacyProject.Add(file);
                node.Children.Add(BuildLegacyFileNode(file));
            }
            catch (Exception ex)
            {
                StatusText = Localization.Format("Status.Skipped", f, ex.Message);
            }
        }
        return node;
    }

    private LegacyFileNode BuildLegacyFileNode(LegacyMapConfigFile file)
    {
        var root = new LegacyFileNode(file);

        var groupsNode = new LegacyCategoryNode(file, CategoryKind.Groups);
        foreach (var g in file.Groups)
            groupsNode.Children.Add(new LegacyGroupNode(file, g));
        root.Children.Add(groupsNode);

        var mapsNode = new LegacyCategoryNode(file, CategoryKind.Maps);
        foreach (var m in file.Maps)
            mapsNode.Children.Add(new LegacyMapNode(file, m));
        root.Children.Add(mapsNode);

        file.Groups.CollectionChanged += (_, e) =>
            HandleLegacyCollectionChange(e, groupsNode, item => new LegacyGroupNode(file, (LegacyGroupEntry)item));
        file.Maps.CollectionChanged += (_, e) =>
            HandleLegacyCollectionChange(e, mapsNode, item => new LegacyMapNode(file, (LegacyMapEntry)item));

        return root;
    }

    private static void HandleLegacyCollectionChange(NotifyCollectionChangedEventArgs e, TreeNodeBase parent, Func<object, TreeNodeBase> factory)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset) { parent.Children.Clear(); return; }
        if (e.NewItems is not null)
        {
            var insertAt = e.NewStartingIndex >= 0 ? e.NewStartingIndex : parent.Children.Count;
            foreach (var item in e.NewItems)
                parent.Children.Insert(insertAt++, factory(item));
        }
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                var match = parent.Children.FirstOrDefault(c => c switch
                {
                    LegacyMapNode mn => mn.Map == item,
                    LegacyGroupNode gn => gn.Group == item,
                    _ => false,
                });
                if (match is not null) parent.Children.Remove(match);
            }
        }
    }

    private void LegacyAttachDirtyTracking(LegacyMapConfigFile file)
    {
        void MarkDirty() => file.IsDirty = true;

        file.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(LegacyMapConfigFile.IsDirty)) MarkDirty();
        };

        void HookPropertySet(LegacyPropertySet? p)
        {
            if (p is null) return;
            UndoHooks.HookObservable(p, LegacyUndo);
            p.PropertyChanged += (_, _) => MarkDirty();

            UndoHooks.HookCollection(p.GroupSettings, LegacyUndo);
            p.GroupSettings.CollectionChanged += (_, _) => MarkDirty();
            UndoHooks.HookCollection(p.DaysAllowed, LegacyUndo);
            p.DaysAllowed.CollectionChanged += (_, _) => MarkDirty();
            UndoHooks.HookCollection(p.AllowedTimeRanges, LegacyUndo);
            p.AllowedTimeRanges.CollectionChanged += (_, _) => MarkDirty();
            UndoHooks.HookCollection(p.RequiredPermissions, LegacyUndo);
            p.RequiredPermissions.CollectionChanged += (_, _) => MarkDirty();
            UndoHooks.HookCollection(p.AllowedSteamIds, LegacyUndo);
            p.AllowedSteamIds.CollectionChanged += (_, _) => MarkDirty();
            UndoHooks.HookCollection(p.DisallowedSteamIds, LegacyUndo);
            p.DisallowedSteamIds.CollectionChanged += (_, _) => MarkDirty();

            UndoHooks.HookCollection(p.Extras, LegacyUndo);
            p.Extras.CollectionChanged += (_, e) =>
            {
                MarkDirty();
                if (e.NewItems is not null)
                    foreach (LegacyExtraSection sec in e.NewItems) HookExtraSection(sec);
            };
            foreach (var sec in p.Extras) HookExtraSection(sec);
        }

        void HookExtraSection(LegacyExtraSection sec)
        {
            UndoHooks.HookObservable(sec, LegacyUndo);
            UndoHooks.HookCollection(sec.Entries, LegacyUndo);
            sec.Entries.CollectionChanged += (_, e) =>
            {
                MarkDirty();
                if (e.NewItems is not null)
                    foreach (LegacyExtraKeyValue kv in e.NewItems) UndoHooks.HookObservable(kv, LegacyUndo);
            };
            foreach (var kv in sec.Entries) UndoHooks.HookObservable(kv, LegacyUndo);
        }

        HookPropertySet(file.DefaultSettings);
        foreach (var g in file.Groups) HookGroup(g, MarkDirty, HookPropertySet);
        foreach (var m in file.Maps) HookMap(m, MarkDirty, HookPropertySet);

        UndoHooks.HookCollection(file.Groups, LegacyUndo);
        file.Groups.CollectionChanged += (_, e) =>
        {
            MarkDirty();
            if (e.NewItems is not null) foreach (LegacyGroupEntry g in e.NewItems) HookGroup(g, MarkDirty, HookPropertySet);
        };

        UndoHooks.HookCollection(file.Maps, LegacyUndo);
        file.Maps.CollectionChanged += (_, e) =>
        {
            MarkDirty();
            if (e.NewItems is not null) foreach (LegacyMapEntry m in e.NewItems) HookMap(m, MarkDirty, HookPropertySet);
        };
    }

    private void HookGroup(LegacyGroupEntry g, Action markDirty, Action<LegacyPropertySet?> hookProps)
    {
        UndoHooks.HookObservable(g, LegacyUndo);
        g.PropertyChanged += (_, _) => markDirty();
        hookProps(g.Properties);
    }

    private void HookMap(LegacyMapEntry m, Action markDirty, Action<LegacyPropertySet?> hookProps)
    {
        UndoHooks.HookObservable(m, LegacyUndo);
        m.PropertyChanged += (_, _) => markDirty();
        hookProps(m.Properties);
    }

    private void LegacySaveFile(LegacyMapConfigFile file)
    {
        try
        {
            if (string.IsNullOrEmpty(file.FilePath))
            {
                _ = LegacySaveAsAsync();
                return;
            }
            LegacyConfigWriter.SaveFile(file);
            StatusText = Localization.Format("Status.Saved", file.FilePath);
        }
        catch (Exception ex)
        {
            Log.Error("LegacyFile", $"Save failed for {file.DisplayName}: {ex.Message}");
            StatusText = Localization.Format("Status.SaveFailed", ex.Message);
        }
    }

    private async Task LegacySaveAsAsync()
    {
        if (SelectedNode is null) return;
        var file = GetLegacyFileForNode(SelectedNode);
        if (file is null) return;

        var top = GetTopLevelInternal();
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
        LegacySaveFile(file);
    }

    public void LegacyNavigateToMap(LegacyMapEntry map) =>
        SelectAndExpandInternal(n => n is LegacyMapNode mn && mn.Map == map);

    public void LegacyNavigateToGroup(LegacyGroupEntry group) =>
        SelectAndExpandInternal(n => n is LegacyGroupNode gn && gn.Group == group);

    public void LegacyNavigateToDefault() =>
        SelectAndExpandInternal(n => n is LegacyProjectDefaultNode);

    private static LegacyMapConfigFile? GetLegacyFileForNode(TreeNodeBase node) => node switch
    {
        LegacyFileNode f => f.File,
        LegacyCategoryNode c => c.File,
        LegacyMapNode m => m.File,
        LegacyGroupNode g => g.File,
        _ => null,
    };

    /// <summary>
    /// Legacy-mode dispatch entry points called from the existing File-menu commands. The
    /// existing commands branch on Mode and delegate here so the menu wiring stays single-
    /// owned.
    /// </summary>
    internal void LegacyDispatchSave()
    {
        if (SelectedNode is null) return;
        var file = GetLegacyFileForNode(SelectedNode);
        if (file is null) return;
        LegacySaveFile(file);
    }

    internal Task LegacyDispatchSaveAsAsync() => LegacySaveAsAsync();

    internal async Task LegacyDispatchSaveAllAsync()
    {
        var dirtyFiles = EnumerateLegacyFileNodes(Tree)
            .Select(n => n.File)
            .Where(f => f.IsDirty)
            .ToList();

        if (dirtyFiles.Count == 0)
        {
            StatusText = Localization.Get("Status.SaveAllNoDirty");
            return;
        }

        if (GetTopLevelInternal() is Window owner)
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

        foreach (var f in dirtyFiles) LegacySaveFile(f);
        StatusText = Localization.Format("Status.SaveAllDone", dirtyFiles.Count);
        Log.Info("LegacyFile", $"SaveAll: {dirtyFiles.Count} dirty file(s) saved");
    }

    private static IEnumerable<LegacyFileNode> EnumerateLegacyFileNodes(IEnumerable<TreeNodeBase> nodes)
    {
        foreach (var n in nodes)
        {
            if (n is LegacyFileNode fn) yield return fn;
            else
                foreach (var sub in EnumerateLegacyFileNodes(n.Children)) yield return sub;
        }
    }

    internal async Task LegacyDispatchCloseAllAsync()
    {
        if (Tree.Count == 0) return;
        if (GetTopLevelInternal() is not Window owner) return;

        var dirty = LegacyProject.Files.Where(f => f.IsDirty).Select(f => f.DisplayName).ToList();
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
        _legacyProjectDefaultNode = null;
        _legacyPinnedMapsTomlNode = null;
        for (var i = LegacyProject.Files.Count - 1; i >= 0; i--)
            LegacyProject.Remove(LegacyProject.Files[i]);
        LegacyUndo.Clear();
        SelectedNode = null;
        CurrentEditor = new MapChooserSharpMSEditor.ViewModels.Editors.WelcomeViewModel();
        StatusText = Localization.Get("Status.Ready");
    }

    internal async Task LegacyDispatchCloseFileAsync()
    {
        if (SelectedNode is null) return;
        var file = GetLegacyFileForNode(SelectedNode);
        if (file is null) return;
        var fileNode = EnumerateLegacyFileNodes(Tree).FirstOrDefault(n => n.File == file);
        if (fileNode is null) return;
        if (!await ConfirmLegacyDiscardIfDirtyAsync(new[] { fileNode })) return;
        RemoveLegacyFileNodeFromTree(fileNode);
        ResetIfEmptyInternal();
    }

    internal async Task LegacyDispatchRemoveFromViewAsync(TreeNodeBase? node)
    {
        if (node is null) return;
        switch (node)
        {
            case LegacyFileNode fn:
                if (!await ConfirmLegacyDiscardIfDirtyAsync(new[] { fn })) return;
                RemoveLegacyFileNodeFromTree(fn);
                break;
            case FolderNode fld:
                var filesInside = EnumerateLegacyFileNodes(new[] { (TreeNodeBase)fld }).ToList();
                if (!await ConfirmLegacyDiscardIfDirtyAsync(filesInside)) return;
                foreach (var f in filesInside) RemoveLegacyFileNodeFromTree(f);
                if (Tree.Contains(fld)) Tree.Remove(fld);
                break;
            default:
                return;
        }
        ResetIfEmptyInternal();
    }

    private void RemoveLegacyFileNodeFromTree(LegacyFileNode fileNode)
    {
        LegacyProject.Remove(fileNode.File);
        LegacyUndo.Clear();
        if (_legacyPinnedMapsTomlNode == fileNode) _legacyPinnedMapsTomlNode = null;
        var parent = FindLegacyParentFolder(fileNode);
        if (parent is not null)
        {
            parent.Children.Remove(fileNode);
            while (parent is not null && parent.Children.Count == 0)
            {
                var grand = FindLegacyParentFolderOfFolder(parent);
                if (grand is null) { Tree.Remove(parent); parent = null; }
                else { grand.Children.Remove(parent); parent = grand; }
            }
        }
        else
        {
            Tree.Remove(fileNode);
        }
    }

    private FolderNode? FindLegacyParentFolder(LegacyFileNode target) =>
        FindLegacyParentFolderIn(Tree, target);

    private static FolderNode? FindLegacyParentFolderIn(IEnumerable<TreeNodeBase> nodes, LegacyFileNode target)
    {
        foreach (var n in nodes)
        {
            if (n is FolderNode folder)
            {
                if (folder.Children.Contains(target)) return folder;
                var sub = FindLegacyParentFolderIn(folder.Children, target);
                if (sub is not null) return sub;
            }
        }
        return null;
    }

    private FolderNode? FindLegacyParentFolderOfFolder(FolderNode target)
    {
        return FindParentFolderOfFolderInPublic(target);
    }

    private async Task<bool> ConfirmLegacyDiscardIfDirtyAsync(IEnumerable<LegacyFileNode> candidates)
    {
        var dirty = candidates.Where(n => n.File.IsDirty).Select(n => n.File.DisplayName).ToList();
        if (dirty.Count == 0) return true;
        if (GetTopLevelInternal() is not Window owner) return true;
        var message = string.Format(Localization.Get("Confirm.DiscardDirty.Message"), string.Join("\n  • ", dirty));
        return await ConfirmDialog.ShowAsync(owner,
            Localization.Get("Confirm.DiscardDirty.Title"), message,
            Localization.Get("Confirm.DiscardDirty.Yes"),
            Localization.Get("Confirm.DiscardDirty.No"));
    }

    /// <summary>Add a Legacy map or group via the file-routing dialog.</summary>
    internal Task LegacyDispatchAddMapAsync() => LegacyAddEntryAsync(LegacyAddEntryKind.Map);
    internal Task LegacyDispatchAddGroupAsync() => LegacyAddEntryAsync(LegacyAddEntryKind.Group);

    private async Task LegacyAddEntryAsync(LegacyAddEntryKind kind)
    {
        if (GetTopLevelInternal() is not Window owner) return;

        var current = SelectedNode is not null ? GetLegacyFileForNode(SelectedNode) : null;
        current ??= LegacyProject.Files.FirstOrDefault();

        var defaultName = kind == LegacyAddEntryKind.Map
            ? UniqueNameInternal("new_map", n => LegacyProject.Files.Any(f => f.Maps.Any(m => m.MapName == n)))
            : UniqueNameInternal("NewGroup", n => LegacyProject.Files.Any(f => f.Groups.Any(g => g.GroupName == n)));

        var result = await LegacyAddEntryDialog.ShowAsync(
            owner,
            kind,
            LegacyProject.Files.ToList(),
            current,
            defaultName,
            async () =>
            {
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
                return CreateLegacyFileAtPath(path);
            });

        if (result is null) return;

        if (kind == LegacyAddEntryKind.Map)
        {
            var map = new LegacyMapEntry { MapName = result.Name };
            result.Target.Maps.Add(map);
            Log.Info("LegacyTree", $"Added Map '{result.Name}' → {result.Target.DisplayName}");
            LegacyNavigateToMap(map);
        }
        else
        {
            var group = new LegacyGroupEntry { GroupName = result.Name };
            result.Target.Groups.Add(group);
            Log.Info("LegacyTree", $"Added Group '{result.Name}' → {result.Target.DisplayName}");
            LegacyNavigateToGroup(group);
        }
    }

    private LegacyMapConfigFile CreateLegacyFileAtPath(string path)
    {
        var file = new LegacyMapConfigFile
        {
            FilePath = path,
            DisplayName = Path.GetFileName(path),
        };
        LegacyAttachDirtyTracking(file);
        LegacyProject.Add(file);
        file.IsDirty = true;
        var node = BuildLegacyFileNode(file);

        // Mirror the Current-side behavior: if the new file lives under a loaded root
        // folder, drop it into the matching subfolder hierarchy (creating missing
        // intermediate folder nodes) rather than orphaning it at the tree root.
        var folder = FindOrCreateFolderForInternal(path);
        if (folder is not null)
        {
            InsertLegacyFileSorted(folder, node);
            folder.IsExpanded = true;
        }
        else
        {
            Tree.Add(node);
        }
        return file;
    }

    private static void InsertLegacyFileSorted(FolderNode parent, LegacyFileNode newFile)
    {
        var idx = 0;
        foreach (var c in parent.Children)
        {
            if (c is FolderNode) { idx++; continue; }
            if (c is LegacyFileNode existing
                && StringComparer.OrdinalIgnoreCase.Compare(existing.File.DisplayName, newFile.File.DisplayName) <= 0)
            {
                idx++;
                continue;
            }
            break;
        }
        parent.Children.Insert(idx, newFile);
    }

    // ===== Search / WorkshopCheck windows =====

    private Views.Legacy.LegacySearchWindow? _legacySearchWindow;

    [RelayCommand(CanExecute = nameof(CanOpenLegacySearch))]
    private void OpenLegacySearch()
    {
        if (_legacySearchWindow is { IsVisible: true })
        {
            _legacySearchWindow.Activate();
            return;
        }
        _legacySearchWindow = new Views.Legacy.LegacySearchWindow { DataContext = LegacySearch };
        _legacySearchWindow.Closed += (_, _) => _legacySearchWindow = null;
        if (GetTopLevelInternal() is Window owner) _legacySearchWindow.Show(owner);
        else _legacySearchWindow.Show();
    }

    public void CloseLegacySearchWindow() => _legacySearchWindow?.Close();
    private bool CanOpenLegacySearch() => Mode == AppMode.Legacy;

    [RelayCommand(CanExecute = nameof(CanOpenLegacyWorkshop))]
    private async Task OpenLegacyWorkshopCheckAsync()
    {
        if (GetTopLevelInternal() is not Window owner) return;
        var vm = new LegacyWorkshopCheckViewModel(LegacyProject);
        var dlg = new Views.Legacy.LegacyWorkshopCheckWindow { DataContext = vm };
        await dlg.ShowDialog(owner);
        if (dlg.Tag is int applied && applied > 0)
        {
            Log.Info("LegacyWorkshop", $"Applied Disabled=true to {applied} map(s)");
            StatusText = Localization.Format("WorkshopCheck.Applied", applied);
        }
    }

    private bool CanOpenLegacyWorkshop() => Mode == AppMode.Legacy;

    public void LegacyNavigateToSearchResult(LegacySearchResult r)
    {
        Log.Debug("LegacyNavigate", $"Search result → {r.Kind}: {r.Label} ({r.FileName})");
        switch (r.Kind)
        {
            case LegacySearchResultKind.Default: LegacyNavigateToDefault(); break;
            case LegacySearchResultKind.Group when r.Target is LegacyGroupEntry g: LegacyNavigateToGroup(g); break;
            case LegacySearchResultKind.Map when r.Target is LegacyMapEntry m: LegacyNavigateToMap(m); break;
        }
    }
}

// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.Services.Legacy;

namespace MapChooserSharpMSEditor.ViewModels.Legacy;

public enum LegacyDiffEntryKind { Default, Group, Map }

public enum LegacyDiffStatus { Unchanged, Added, Deleted, Modified }

/// <summary>One entry (Default / Group / Map) selectable from the diff window's picker.
/// Status is computed once at snapshot-load time (not on demand) so the list can paint
/// add/delete/modify colors and the "changed only" filter can skip unchanged rows.</summary>
public sealed record LegacyDiffEntry(
    LegacyDiffEntryKind Kind,
    string Key,
    bool HasLeft,
    bool HasRight,
    LegacyDiffStatus Status)
{
    public string KindLabel => Kind switch
    {
        LegacyDiffEntryKind.Default => "Default",
        LegacyDiffEntryKind.Group => "Group",
        _ => "Map",
    };

    public string PresenceTag => Status switch
    {
        LegacyDiffStatus.Added => "added",
        LegacyDiffStatus.Deleted => "deleted",
        LegacyDiffStatus.Modified => "modified",
        _ => "",
    };

    // Direction: left = current branch (HEAD), right = compare target. Git-diff
    // convention — an entry present on HEAD but not target = "added by this branch".
    public bool IsAdded => Status == LegacyDiffStatus.Added;
    public bool IsDeleted => Status == LegacyDiffStatus.Deleted;
    public bool IsModified => Status == LegacyDiffStatus.Modified;
    public bool IsUnchanged => Status == LegacyDiffStatus.Unchanged;
}

public sealed record LegacyDiffRow(
    string PropertyKey,
    string Label,
    string LeftValue,
    string LeftSource,
    string RightValue,
    string RightSource,
    bool Differs);

public sealed partial class LegacyBranchDiffViewModel : ViewModelBase
{
    private readonly LegacyProjectContext _leftProject;
    private LegacyProjectContext? _rightProject;

    public string? RepoRoot { get; }
    public string? CurrentBranch { get; }

    [ObservableProperty] private ObservableCollection<string> _availableBranches = new();
    [ObservableProperty] private string? _selectedBranch;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _snapshotWarningText = "";

    [ObservableProperty] private string _entryFilter = "";
    [ObservableProperty] private bool _showChangedOnly = true;

    public ObservableCollection<LegacyDiffEntry> Entries { get; } = new();
    public ObservableCollection<LegacyDiffEntry> FilteredEntries { get; } = new();
    [ObservableProperty] private LegacyDiffEntry? _selectedEntry;

    [ObservableProperty] private int _addedCount;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private int _modifiedCount;
    [ObservableProperty] private int _unchangedCount;

    public ObservableCollection<LegacyDiffRow> Rows { get; } = new();
    [ObservableProperty] private string _leftHeading = "";
    [ObservableProperty] private string _rightHeading = "";
    [ObservableProperty] private int _differCount;

    public LegacyBranchDiffViewModel(LegacyProjectContext project)
    {
        _leftProject = project;

        // Find repo root from any loaded file path. If config dir itself isn't a git
        // repo, walk up through parents — the user explicitly asked for this fallback.
        RepoRoot = project.Files
            .Select(f => f.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => GitService.FindRepoRoot(p!))
            .FirstOrDefault(r => r is not null);

        if (RepoRoot is not null)
        {
            CurrentBranch = GitService.GetCurrentBranch(RepoRoot);
            foreach (var b in GitService.ListBranches(RepoRoot))
                AvailableBranches.Add(b);
            // Default to something that isn't the current branch if possible.
            SelectedBranch = AvailableBranches.FirstOrDefault(b => b != CurrentBranch) ?? AvailableBranches.FirstOrDefault();
            StatusText = $"Repo: {RepoRoot}  ·  current: {CurrentBranch ?? "(detached)"}";
        }
        else
        {
            StatusText = "No git repo found above the loaded config files.";
        }
    }

    partial void OnEntryFilterChanged(string value) => RebuildFilteredEntries();
    partial void OnShowChangedOnlyChanged(bool value) => RebuildFilteredEntries();
    partial void OnSelectedEntryChanged(LegacyDiffEntry? value) => RebuildRows();

    [RelayCommand]
    private async Task LoadSnapshotAsync()
    {
        if (RepoRoot is null || string.IsNullOrEmpty(SelectedBranch)) return;
        IsLoading = true;
        try
        {
            // git show blocks on disk I/O per file; offload to a background thread so the
            // window keeps responding when there are lots of files.
            var snapshot = await Task.Run(() =>
                LegacyBranchSnapshotLoader.Load(_leftProject, RepoRoot, SelectedBranch));

            _rightProject = snapshot.Project;
            SnapshotWarningText = snapshot.Warnings.Count == 0
                ? ""
                : $"{snapshot.Warnings.Count} warning(s): " + string.Join(" / ", snapshot.Warnings.Take(3))
                  + (snapshot.Warnings.Count > 3 ? " …" : "");
            StatusText = $"{SelectedBranch}: loaded {snapshot.LoadedCount} file(s), missing {snapshot.MissingCount}.";
            RebuildEntries();
            RebuildRows();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildEntries()
    {
        Entries.Clear();
        var seen = new HashSet<(LegacyDiffEntryKind, string)>();
        AddedCount = DeletedCount = ModifiedCount = UnchangedCount = 0;

        void AddIfNew(LegacyDiffEntryKind k, string key, bool hasLeft, bool hasRight)
        {
            var tuple = (k, key);
            if (!seen.Add(tuple)) return;

            // Classify once: Added/Deleted by presence only, Modified/Unchanged by
            // comparing effective values on both sides (computed eagerly because we need
            // it for the list filter anyway).
            LegacyDiffStatus status;
            if (!hasLeft && hasRight) status = LegacyDiffStatus.Added; // present only on compare target → "added by this branch"
            else if (hasLeft && !hasRight) status = LegacyDiffStatus.Deleted;
            else
            {
                var entry = new LegacyDiffEntry(k, key, hasLeft, hasRight, LegacyDiffStatus.Unchanged);
                status = ComputeBothSidesDiffer(entry) ? LegacyDiffStatus.Modified : LegacyDiffStatus.Unchanged;
            }

            Entries.Add(new LegacyDiffEntry(k, key, hasLeft, hasRight, status));
            switch (status)
            {
                case LegacyDiffStatus.Added: AddedCount++; break;
                case LegacyDiffStatus.Deleted: DeletedCount++; break;
                case LegacyDiffStatus.Modified: ModifiedCount++; break;
                case LegacyDiffStatus.Unchanged: UnchangedCount++; break;
            }
        }

        var leftHasDefault = _leftProject.DefaultOwner is not null;
        var rightHasDefault = _rightProject?.DefaultOwner is not null;
        if (leftHasDefault || rightHasDefault)
            AddIfNew(LegacyDiffEntryKind.Default, "Default", leftHasDefault, rightHasDefault);

        var rightGroups = _rightProject is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(_rightProject.Files.SelectMany(f => f.Groups).Select(g => g.GroupName), StringComparer.OrdinalIgnoreCase);
        var rightMaps = _rightProject is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(_rightProject.Files.SelectMany(f => f.Maps).Select(m => m.MapName), StringComparer.OrdinalIgnoreCase);

        var leftGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in _leftProject.Files.SelectMany(f => f.Groups).Select(g => g.GroupName))
        {
            leftGroups.Add(g);
            AddIfNew(LegacyDiffEntryKind.Group, g, true, rightGroups.Contains(g));
        }
        foreach (var g in rightGroups)
            if (!leftGroups.Contains(g))
                AddIfNew(LegacyDiffEntryKind.Group, g, false, true);

        var leftMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _leftProject.Files.SelectMany(f => f.Maps).Select(m => m.MapName))
        {
            leftMaps.Add(m);
            AddIfNew(LegacyDiffEntryKind.Map, m, true, rightMaps.Contains(m));
        }
        foreach (var m in rightMaps)
            if (!leftMaps.Contains(m))
                AddIfNew(LegacyDiffEntryKind.Map, m, false, true);

        RebuildFilteredEntries();
        SelectedEntry = FilteredEntries.FirstOrDefault();
    }

    /// <summary>Cheap change-detect for an entry present on both sides: compare the
    /// effective rows by DisplayValue.</summary>
    private bool ComputeBothSidesDiffer(LegacyDiffEntry entry)
    {
        if (_rightProject is null) return false;
        var left = ResolveFor(entry, _leftProject);
        var right = ResolveFor(entry, _rightProject);
        if (left.Count == 0 && right.Count == 0) return false;
        var rightByKey = right.ToDictionary(r => r.Property, r => r.DisplayValue);
        foreach (var l in left)
        {
            if (!rightByKey.TryGetValue(l.Property, out var rv) || rv != l.DisplayValue) return true;
        }
        // Also catch right-only keys (shouldn't happen since property name list is
        // identical in LegacyPropertyResolver, but defensive).
        if (right.Count != left.Count) return true;
        return false;
    }

    private void RebuildFilteredEntries()
    {
        FilteredEntries.Clear();
        var needle = EntryFilter?.Trim() ?? "";
        foreach (var e in Entries)
        {
            if (ShowChangedOnly && e.IsUnchanged) continue;
            if (!string.IsNullOrEmpty(needle) && !e.Key.Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;
            FilteredEntries.Add(e);
        }
    }

    private void RebuildRows()
    {
        Rows.Clear();
        DifferCount = 0;
        LeftHeading = CurrentBranch ?? "current";
        RightHeading = SelectedBranch ?? "?";

        if (SelectedEntry is null || _rightProject is null) return;

        var leftRows = ResolveFor(SelectedEntry, _leftProject);
        var rightRows = ResolveFor(SelectedEntry, _rightProject);

        // Pair by property name. Both sides use PropertyNames in the same order so a
        // simple zip is enough; keep it tolerant in case an entry is missing on one side.
        var byKey = new Dictionary<string, (LegacyPropertyResolver.ResolvedRow? L, LegacyPropertyResolver.ResolvedRow? R)>();
        foreach (var r in leftRows) byKey[r.Property] = (r, byKey.GetValueOrDefault(r.Property).R);
        foreach (var r in rightRows)
        {
            var existing = byKey.GetValueOrDefault(r.Property);
            byKey[r.Property] = (existing.L, r);
        }

        var differs = 0;
        foreach (var key in byKey.Keys)
        {
            var (l, r) = byKey[key];
            var leftVal = l?.DisplayValue ?? "—";
            var rightVal = r?.DisplayValue ?? "—";
            var label = l?.Label ?? r?.Label ?? key;
            var leftSrc = l?.Source ?? "";
            var rightSrc = r?.Source ?? "";
            var diff = !string.Equals(leftVal, rightVal, StringComparison.Ordinal);
            if (diff) differs++;
            Rows.Add(new LegacyDiffRow(key, label, leftVal, leftSrc, rightVal, rightSrc, diff));
        }
        DifferCount = differs;
    }

    private static List<LegacyPropertyResolver.ResolvedRow> ResolveFor(LegacyDiffEntry entry, LegacyProjectContext project)
    {
        switch (entry.Kind)
        {
            case LegacyDiffEntryKind.Default:
                var owner = project.DefaultOwner;
                return owner is null
                    ? new List<LegacyPropertyResolver.ResolvedRow>()
                    : LegacyPropertyResolver.ResolveDefault(owner);
            case LegacyDiffEntryKind.Group:
                var g = project.Files.SelectMany(f => f.Groups)
                    .FirstOrDefault(x => string.Equals(x.GroupName, entry.Key, StringComparison.OrdinalIgnoreCase));
                if (g is null) return new();
                var gFile = project.Files.First(f => f.Groups.Contains(g));
                return LegacyPropertyResolver.ResolveGroup(g, gFile, project);
            case LegacyDiffEntryKind.Map:
                var m = project.Files.SelectMany(f => f.Maps)
                    .FirstOrDefault(x => string.Equals(x.MapName, entry.Key, StringComparison.OrdinalIgnoreCase));
                if (m is null) return new();
                var mFile = project.Files.First(f => f.Maps.Contains(m));
                return LegacyPropertyResolver.ResolveMap(m, mFile, project);
        }
        return new();
    }
}

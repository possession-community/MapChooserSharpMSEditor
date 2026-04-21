using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels;

/// <summary>One row in the Workshop-check results table: a map whose workshop item
/// is private or deleted, paired with a checkbox so the user can pick which ones to
/// actually flip to <c>IsDisabled = true</c>.</summary>
public sealed partial class WorkshopCheckCandidate : ObservableObject
{
    public MapEntryModel Map { get; }
    public MapConfigFile File { get; }
    public ulong WorkshopId { get; }
    public WorkshopStatus Status { get; }
    public string? FailureReason { get; }

    [ObservableProperty] private bool _isSelected = true;

    public string StatusLabel => Status switch
    {
        WorkshopStatus.NotFoundOrPrivate => "not found / private",
        WorkshopStatus.Error => FailureReason is null ? "error" : $"error: {FailureReason}",
        _ => "public",
    };

    public WorkshopCheckCandidate(MapEntryModel map, MapConfigFile file, ulong workshopId, WorkshopStatus status, string? failureReason)
    {
        Map = map;
        File = file;
        WorkshopId = workshopId;
        Status = status;
        FailureReason = failureReason;
    }
}

/// <summary>
/// Backs the WorkshopCheckWindow. Iterates every loaded map that has a WorkshopId set
/// (and isn't already IsDisabled=true), asks Steam whether each item is still public,
/// and lists the non-public ones with a per-row checkbox for Disabled promotion.
/// </summary>
public sealed partial class WorkshopCheckViewModel : ViewModelBase
{
    private readonly ProjectContext _project;
    private readonly WorkshopCheckService _service = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private int _progressDone;
    [ObservableProperty] private int _progressTotal;

    /// <summary>Rows returned by the last run. Starts empty; Run populates.</summary>
    public ObservableCollection<WorkshopCheckCandidate> Candidates { get; } = new();

    public bool HasCandidates => Candidates.Count > 0;
    public bool HasCompletedRun { get; private set; }
    public bool ShowEmptyMessage => HasCompletedRun && Candidates.Count == 0 && !IsRunning;

    public WorkshopCheckViewModel(ProjectContext project)
    {
        _project = project;
        Candidates.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasCandidates));
            OnPropertyChanged(nameof(ShowEmptyMessage));
        };
    }

    /// <summary>
    /// Gathers every (map, workshopId) pair from the project and asks Steam for status.
    /// Only non-public items are surfaced, and already-disabled maps are skipped because
    /// the whole point of the feature is flagging ones that <i>aren't</i> disabled yet.
    /// </summary>
    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning) return;

        Candidates.Clear();
        HasCompletedRun = false;
        OnPropertyChanged(nameof(ShowEmptyMessage));

        var targets = new List<(MapEntryModel Map, MapConfigFile File, ulong Id)>();
        foreach (var file in _project.Files)
        {
            foreach (var m in file.Maps)
            {
                if (!m.Properties.HasWorkshopId || m.Properties.WorkshopId <= 0) continue;
                // Skip maps the user already marked disabled — they're not actionable here.
                if (m.Properties.HasIsDisabled && m.Properties.IsDisabled) continue;
                targets.Add((m, file, (ulong)m.Properties.WorkshopId));
            }
        }

        if (targets.Count == 0)
        {
            StatusText = Localization.Get("WorkshopCheck.NoTargets");
            HasCompletedRun = true;
            OnPropertyChanged(nameof(ShowEmptyMessage));
            return;
        }

        IsRunning = true;
        ProgressDone = 0;
        ProgressTotal = targets.Count;
        StatusText = Localization.Format("WorkshopCheck.Running", 0, targets.Count);

        try
        {
            var progress = new Progress<(int done, int total)>(p =>
            {
                ProgressDone = p.done;
                ProgressTotal = p.total;
                StatusText = Localization.Format("WorkshopCheck.Running", p.done, p.total);
            });

            var ids = targets.Select(t => t.Id).ToList();
            var results = await _service.CheckAsync(ids, progress);

            // Index results by id so we can zip with our original target list (in input
            // order) without re-walking Steam responses per entry.
            var byId = results.ToDictionary(r => r.PublishedFileId, r => r);
            foreach (var t in targets)
            {
                if (!byId.TryGetValue(t.Id, out var r)) continue;
                if (r.Status == WorkshopStatus.Public) continue;
                Candidates.Add(new WorkshopCheckCandidate(t.Map, t.File, t.Id, r.Status, r.ErrorMessage));
            }

            StatusText = Candidates.Count == 0
                ? Localization.Get("WorkshopCheck.AllPublic")
                : Localization.Format("WorkshopCheck.FoundNonPublic", Candidates.Count);
        }
        catch (Exception ex)
        {
            StatusText = Localization.Format("WorkshopCheck.Failed", ex.Message);
        }
        finally
        {
            IsRunning = false;
            HasCompletedRun = true;
            OnPropertyChanged(nameof(ShowEmptyMessage));
        }
    }

    /// <summary>Select all / none shortcuts for the result list.</summary>
    [RelayCommand] private void SelectAll()   { foreach (var c in Candidates) c.IsSelected = true; }
    [RelayCommand] private void SelectNone()  { foreach (var c in Candidates) c.IsSelected = false; }

    /// <summary>
    /// Flip <see cref="PropertySet.IsDisabled"/> = true on every checked row and mark
    /// the owning files dirty so Save has something to write. Returns the count so the
    /// view can close afterwards with a status line.
    /// </summary>
    public int ApplySelection()
    {
        var touched = 0;
        foreach (var c in Candidates)
        {
            if (!c.IsSelected) continue;
            // Setting the scalar property also flips HasIsDisabled via PropertySet's auto-set
            // partials, so the saved TOML will include the explicit `IsDisabled = true`.
            c.Map.Properties.IsDisabled = true;
            c.File.IsDirty = true;
            touched++;
        }
        return touched;
    }
}

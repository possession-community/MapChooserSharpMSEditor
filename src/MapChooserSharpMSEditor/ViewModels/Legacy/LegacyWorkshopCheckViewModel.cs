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

namespace MapChooserSharpMSEditor.ViewModels.Legacy;

public sealed partial class LegacyWorkshopCheckCandidate : ObservableObject
{
    public LegacyMapEntry Map { get; }
    public LegacyMapConfigFile File { get; }
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

    public LegacyWorkshopCheckCandidate(LegacyMapEntry map, LegacyMapConfigFile file, ulong workshopId, WorkshopStatus status, string? failureReason)
    {
        Map = map;
        File = file;
        WorkshopId = workshopId;
        Status = status;
        FailureReason = failureReason;
    }
}

public sealed partial class LegacyWorkshopCheckViewModel : ViewModelBase
{
    private readonly LegacyProjectContext _project;
    private readonly WorkshopCheckService _service = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private int _progressDone;
    [ObservableProperty] private int _progressTotal;

    public ObservableCollection<LegacyWorkshopCheckCandidate> Candidates { get; } = new();

    public bool HasCandidates => Candidates.Count > 0;
    public bool HasCompletedRun { get; private set; }
    public bool ShowEmptyMessage => HasCompletedRun && Candidates.Count == 0 && !IsRunning;

    public LegacyWorkshopCheckViewModel(LegacyProjectContext project)
    {
        _project = project;
        Candidates.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasCandidates));
            OnPropertyChanged(nameof(ShowEmptyMessage));
        };
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning) return;

        Candidates.Clear();
        HasCompletedRun = false;
        OnPropertyChanged(nameof(ShowEmptyMessage));

        var targets = new List<(LegacyMapEntry Map, LegacyMapConfigFile File, ulong Id)>();
        foreach (var file in _project.Files)
        {
            foreach (var m in file.Maps)
            {
                if (!m.Properties.HasWorkshopId || m.Properties.WorkshopId <= 0) continue;
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

            var byId = results.ToDictionary(r => r.PublishedFileId, r => r);
            foreach (var t in targets)
            {
                if (!byId.TryGetValue(t.Id, out var r)) continue;
                if (r.Status == WorkshopStatus.Public) continue;
                Candidates.Add(new LegacyWorkshopCheckCandidate(t.Map, t.File, t.Id, r.Status, r.ErrorMessage));
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

    [RelayCommand] private void SelectAll()  { foreach (var c in Candidates) c.IsSelected = true; }
    [RelayCommand] private void SelectNone() { foreach (var c in Candidates) c.IsSelected = false; }

    public int ApplySelection()
    {
        var touched = 0;
        foreach (var c in Candidates)
        {
            if (!c.IsSelected) continue;
            c.Map.Properties.IsDisabled = true;
            c.File.IsDirty = true;
            touched++;
        }
        return touched;
    }
}

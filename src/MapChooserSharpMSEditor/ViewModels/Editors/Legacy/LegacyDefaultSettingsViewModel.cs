// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

/// <summary>
/// Project-wide Default editor for Legacy. Mirrors DefaultSettingsViewModel: at most one
/// file may own the [MapChooserSharpSettings.Default] section, so the UI either shows the
/// current owner's PropertySet (with a "detach" button) or a file-picker to assign it.
/// </summary>
public sealed partial class LegacyDefaultSettingsViewModel : ViewModelBase
{
    public LegacyProjectContext Project { get; }

    public LegacyDefaultSettingsViewModel(LegacyProjectContext project)
    {
        Project = project;
        RebuildState();
        Project.Files.CollectionChanged += (_, _) => RebuildState();
        Project.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LegacyProjectContext.DefaultOwner)
                or nameof(LegacyProjectContext.DefaultOwners)
                or nameof(LegacyProjectContext.HasMultipleDefaults))
                RebuildState();
        };
    }

    [ObservableProperty] private LegacyMapConfigFile? _owner;
    [ObservableProperty] private LegacyPropertySetViewModel? _properties;
    [ObservableProperty] private bool _hasOwner;
    [ObservableProperty] private bool _hasMultiple;
    [ObservableProperty] private string _assignFilter = string.Empty;

    public ObservableCollection<LegacyMapConfigFile> FilteredFiles { get; } = new();
    public IReadOnlyList<LegacyMapConfigFile> DefaultOwners => Project.DefaultOwners;

    partial void OnAssignFilterChanged(string value) => RebuildFilteredFiles();

    private void RebuildState()
    {
        Owner = Project.DefaultOwner;
        HasOwner = Owner is not null;
        HasMultiple = Project.HasMultipleDefaults;
        Properties = Owner?.DefaultSettings is null
            ? null
            : new LegacyPropertySetViewModel(Owner.DefaultSettings, LegacyPropertyScope.Default, Project);
        RebuildFilteredFiles();
        OnPropertyChanged(nameof(DefaultOwners));
    }

    private void RebuildFilteredFiles()
    {
        FilteredFiles.Clear();
        var needle = AssignFilter?.Trim() ?? string.Empty;
        foreach (var f in Project.Files)
        {
            if (string.IsNullOrEmpty(needle)
                || f.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase))
                FilteredFiles.Add(f);
        }
    }

    [RelayCommand]
    private void Assign(LegacyMapConfigFile? file)
    {
        if (file is null) return;
        if (file.DefaultSettings is null)
        {
            using var _ = Project.Undo.BeginBatch("Assign Default");
            file.DefaultSettings = new LegacyPropertySet();
        }
        RebuildState();
    }

    [RelayCommand]
    private void DetachFromFile(LegacyMapConfigFile? file)
    {
        if (file is null) return;
        using var _ = Project.Undo.BeginBatch("Detach Default");
        file.DefaultSettings = null;
        RebuildState();
    }
}

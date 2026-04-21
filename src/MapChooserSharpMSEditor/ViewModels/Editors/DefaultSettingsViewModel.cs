using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

/// <summary>
/// Editor for the project-wide <c>[MapChooserSharpSettings.Default]</c> section. Because the
/// server treats Default as a singleton (the TOML parser rejects duplicate sections), this
/// VM always targets whatever file currently <i>owns</i> the section. Three states:
/// <list type="bullet">
///   <item><b>Owner exists:</b> edit its DefaultSettings directly, showing the filename
///   so the user knows where the section is persisted.</item>
///   <item><b>No owner yet:</b> surface an "Assign to file…" dropdown + button; picking a
///   file attaches a fresh <see cref="PropertySet"/> to it.</item>
///   <item><b>Multiple owners (loaded from disk with duplicate sections):</b> show a
///   warning — the server won't load this config, the user needs to resolve by removing
///   one.</item>
/// </list>
/// </summary>
public sealed partial class DefaultSettingsViewModel : ViewModelBase
{
    private readonly ProjectContext _project;

    [ObservableProperty] private MapConfigFile? _owner;
    [ObservableProperty] private PropertySetViewModel? _properties;
    [ObservableProperty] private bool _hasMultipleOwners;
    [ObservableProperty] private MapConfigFile? _assignTarget;

    /// <summary>Files eligible to receive a new Default, i.e. every loaded file.</summary>
    public IReadOnlyList<MapConfigFile> AssignableFiles => _project.Files.ToList();

    /// <summary>Every file that currently carries a DefaultSettings — used by the
    /// "duplicate Default" warning banner to list offenders.</summary>
    public IReadOnlyList<MapConfigFile> Owners => _project.DefaultOwners;

    public bool HasOwner => Owner is not null;
    public bool HasNoOwner => Owner is null;

    public DefaultSettingsViewModel(ProjectContext project)
    {
        _project = project;
        project.PropertyChanged += OnProjectChanged;
        project.Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(AssignableFiles));
        Refresh();
    }

    private void OnProjectChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectContext.DefaultOwner)
            or nameof(ProjectContext.DefaultOwners)
            or nameof(ProjectContext.HasMultipleDefaults))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        Owner = _project.DefaultOwner;
        HasMultipleOwners = _project.HasMultipleDefaults;
        Properties = Owner?.DefaultSettings is { } set
            ? new PropertySetViewModel(set, PropertyScope.Default, _project)
            : null;
        OnPropertyChanged(nameof(HasOwner));
        OnPropertyChanged(nameof(HasNoOwner));
        OnPropertyChanged(nameof(Owners));
        // Default AssignTarget to first file so the dropdown isn't empty-looking when
        // we enter the no-owner state.
        AssignTarget ??= _project.Files.FirstOrDefault();
    }

    /// <summary>
    /// Attaches a fresh PropertySet to <see cref="AssignTarget"/>'s DefaultSettings,
    /// making that file the on-disk owner. ProjectContext picks up the change through
    /// its per-file PropertyChanged hook and republishes <c>DefaultOwner</c>, which
    /// triggers <see cref="Refresh"/>.
    /// </summary>
    [RelayCommand]
    private void Assign()
    {
        if (AssignTarget is null) return;
        if (AssignTarget.DefaultSettings is not null) return; // already owner; nothing to do
        AssignTarget.DefaultSettings = new PropertySet();
    }

    /// <summary>
    /// Clears the current owner's Default section. Useful when the user wants to move the
    /// Default to a different file: detach here, then Assign to the new target.
    /// </summary>
    [RelayCommand]
    private void Unassign()
    {
        if (Owner is null) return;
        Owner.DefaultSettings = null;
    }
}

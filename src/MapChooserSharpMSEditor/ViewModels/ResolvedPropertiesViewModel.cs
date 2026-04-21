using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.ViewModels.Editors;

// L is shorthand for Localization used by the tables below; keeps string work on one line.

namespace MapChooserSharpMSEditor.ViewModels;

/// <summary>
/// Right-side read-only preview that shows the effective values the server will consume,
/// given the current selection in the tree. Recomputes on demand via <see cref="Refresh"/>.
/// </summary>
public sealed partial class ResolvedPropertiesViewModel : ObservableObject
{
    public ObservableCollection<PropertyResolver.ResolvedRow> Rows { get; } = new();

    /// <summary>Resolved Extras (collapsible sections), already merged across the
    /// inheritance chain — higher-priority sources win per (section, key).</summary>
    public ObservableCollection<PropertyResolver.ResolvedExtraSection> ExtraSections { get; } = new();

    public bool HasExtraSections => ExtraSections.Count > 0;

    [ObservableProperty] private string _heading = Localization.Get("Resolved.Placeholder.Heading");
    [ObservableProperty] private string _contextLine = Localization.Get("Resolved.Placeholder.Body");

    public void Refresh(ViewModelBase? currentEditor, ProjectContext project)
    {
        Rows.Clear();
        ExtraSections.Clear();

        switch (currentEditor)
        {
            case DefaultSettingsViewModel d:
                Heading = Localization.Get("Resolved.HeadingDefault");
                ContextLine = d.Owner?.DisplayName ?? Localization.Get("Default.NoOwner.Title");
                if (d.Owner is not null)
                {
                    foreach (var r in PropertyResolver.ResolveDefault(d.Owner))
                        Rows.Add(r);
                    foreach (var s in PropertyResolver.ResolveDefaultExtras(d.Owner))
                        ExtraSections.Add(s);
                }
                break;

            case GroupEditorViewModel g:
                Heading = Localization.Format("Resolved.HeadingGroup", g.Group.GroupName);
                ContextLine = g.File.DisplayName;
                foreach (var r in PropertyResolver.ResolveGroup(g.Group, g.File, project))
                    Rows.Add(r);
                foreach (var s in PropertyResolver.ResolveGroupExtras(g.Group, g.File, project))
                    ExtraSections.Add(s);
                break;

            case MapEditorViewModel m:
                Heading = Localization.Format("Resolved.HeadingMap", m.Map.MapName);
                ContextLine = m.File.DisplayName;
                foreach (var r in PropertyResolver.ResolveMap(m.Map, m.File, project))
                    Rows.Add(r);
                foreach (var s in PropertyResolver.ResolveMapExtras(m.Map, m.File, project))
                    ExtraSections.Add(s);
                break;

            case OverrideEditorViewModel o:
                Heading = Localization.Format("Resolved.HeadingOverride", o.Override.Name);
                ContextLine = $"{o.ParentDisplay} / {o.File.DisplayName}";
                (var rows, var extras) = o.Parent switch
                {
                    MapEntryModel map => (
                        PropertyResolver.ResolveMapOverride(o.Override, map, o.File, project),
                        PropertyResolver.ResolveMapOverrideExtras(o.Override, map, o.File, project)),
                    GroupEntryModel grp => (
                        PropertyResolver.ResolveGroupOverride(o.Override, grp, o.File, project),
                        PropertyResolver.ResolveGroupOverrideExtras(o.Override, grp, o.File, project)),
                    _ => (
                        new System.Collections.Generic.List<PropertyResolver.ResolvedRow>(),
                        new System.Collections.Generic.List<PropertyResolver.ResolvedExtraSection>()),
                };
                foreach (var r in rows) Rows.Add(r);
                foreach (var s in extras) ExtraSections.Add(s);
                break;

            default:
                Heading = Localization.Get("Resolved.Placeholder.Heading");
                ContextLine = Localization.Get("Resolved.Placeholder.Body");
                break;
        }

        OnPropertyChanged(nameof(HasExtraSections));
    }
}

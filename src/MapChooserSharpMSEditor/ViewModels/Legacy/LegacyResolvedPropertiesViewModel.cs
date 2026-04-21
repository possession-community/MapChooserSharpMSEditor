// LEGACY — remove when MCS migration completes
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.Services.Legacy;
using MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

namespace MapChooserSharpMSEditor.ViewModels.Legacy;

public sealed partial class LegacyResolvedPropertiesViewModel : ObservableObject
{
    public ObservableCollection<LegacyPropertyResolver.ResolvedRow> Rows { get; } = new();
    public ObservableCollection<LegacyPropertyResolver.ResolvedExtraSection> ExtraSections { get; } = new();

    public bool HasExtraSections => ExtraSections.Count > 0;

    [ObservableProperty] private string _heading = Localization.Get("Resolved.Placeholder.Heading");
    [ObservableProperty] private string _contextLine = Localization.Get("Resolved.Placeholder.Body");

    public void Clear()
    {
        Rows.Clear();
        ExtraSections.Clear();
        Heading = Localization.Get("Resolved.Placeholder.Heading");
        ContextLine = Localization.Get("Resolved.Placeholder.Body");
        OnPropertyChanged(nameof(HasExtraSections));
    }

    public void Refresh(ViewModelBase? currentEditor, LegacyProjectContext project)
    {
        Rows.Clear();
        ExtraSections.Clear();

        switch (currentEditor)
        {
            case LegacyDefaultSettingsViewModel d when d.Owner is not null:
                Heading = Localization.Get("Resolved.HeadingDefault");
                ContextLine = d.Owner.DisplayName;
                foreach (var r in LegacyPropertyResolver.ResolveDefault(d.Owner)) Rows.Add(r);
                foreach (var s in LegacyPropertyResolver.ResolveDefaultExtras(d.Owner)) ExtraSections.Add(s);
                break;

            case LegacyGroupEditorViewModel g:
                Heading = Localization.Format("Resolved.HeadingGroup", g.Group.GroupName);
                ContextLine = g.File.DisplayName;
                foreach (var r in LegacyPropertyResolver.ResolveGroup(g.Group, g.File, project)) Rows.Add(r);
                foreach (var s in LegacyPropertyResolver.ResolveGroupExtras(g.Group, g.File, project)) ExtraSections.Add(s);
                break;

            case LegacyMapEditorViewModel m:
                Heading = Localization.Format("Resolved.HeadingMap", m.Map.MapName);
                ContextLine = m.File.DisplayName;
                foreach (var r in LegacyPropertyResolver.ResolveMap(m.Map, m.File, project)) Rows.Add(r);
                foreach (var s in LegacyPropertyResolver.ResolveMapExtras(m.Map, m.File, project)) ExtraSections.Add(s);
                break;

            default:
                Heading = Localization.Get("Resolved.Placeholder.Heading");
                ContextLine = Localization.Get("Resolved.Placeholder.Body");
                break;
        }

        OnPropertyChanged(nameof(HasExtraSections));
    }
}

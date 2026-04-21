using System;
using System.Collections.Generic;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

public sealed class OverrideEditorViewModel : ViewModelBase
{
    public MapConfigFile File { get; }
    public DaySettingsOverrideModel Override { get; }
    public PropertySetViewModel Properties { get; }

    /// <summary>
    /// One chip per day-of-week mirroring <see cref="DaySettingsOverrideModel.TargetDays"/>.
    /// Lets the view drive the selected-day styling the same way <see cref="PropertySetViewModel.DaysAllowedToggles"/>
    /// does for DaysAllowed.
    /// </summary>
    public IReadOnlyList<DayToggleViewModel> TargetDayToggles { get; }

    /// <summary>
    /// Parent map or group (used for a breadcrumb in the view).
    /// </summary>
    public object Parent { get; }
    public string ParentDisplay { get; }

    public OverrideEditorViewModel(MapConfigFile file, object parent, DaySettingsOverrideModel ov, ProjectContext? project = null)
    {
        File = file;
        Parent = parent;
        Override = ov;
        // Override inherits the scope of its parent so the UI hides the same rows as the
        // parent editor; a GroupDaySettings override can't set MapNameAlias any more than
        // the group itself can.
        var scope = parent switch
        {
            MapEntryModel => PropertyScope.Map,
            GroupEntryModel => PropertyScope.Group,
            _ => PropertyScope.Default,
        };
        // Override inheritance chain skips the override itself and starts with the parent:
        //   map-override  → [map, groups..., default]
        //   group-override → [group, default]
        Properties = new PropertySetViewModel(ov.Properties, scope, project,
            inheritanceChain: () => BuildOverrideChain(file, parent, project),
            inheritedStatusKey: "Status.FromOverrideTarget");
        ParentDisplay = parent switch
        {
            MapEntryModel m => Localization.Format("Editor.ParentMap", m.MapName),
            GroupEntryModel g => Localization.Format("Editor.ParentGroup", g.GroupName),
            _ => "",
        };

        var toggles = new List<DayToggleViewModel>(PropertySetViewModel.AllDays.Length);
        foreach (var d in PropertySetViewModel.AllDays)
            toggles.Add(new DayToggleViewModel(d, ov.TargetDays));
        TargetDayToggles = toggles;
    }

    private static IReadOnlyList<PropertySet> BuildOverrideChain(MapConfigFile file, object parent, ProjectContext? project)
    {
        switch (parent)
        {
            case MapEntryModel map:
            {
                var list = new List<PropertySet> { map.Properties };
                list.AddRange(MapEditorViewModel.BuildMapChain(file, map, project));
                return list;
            }
            case GroupEntryModel group:
            {
                var list = new List<PropertySet> { group.Properties };
                if (file.DefaultSettings is not null) list.Add(file.DefaultSettings);
                return list;
            }
            default:
                return System.Array.Empty<PropertySet>();
        }
    }
}

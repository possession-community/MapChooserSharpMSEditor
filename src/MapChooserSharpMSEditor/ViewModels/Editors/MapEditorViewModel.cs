using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

public sealed partial class MapEditorViewModel : ViewModelBase
{
    public MapConfigFile File { get; }
    public MapEntryModel Map { get; }
    public PropertySetViewModel Properties { get; }

    public MapEditorViewModel(MapConfigFile file, MapEntryModel map, ProjectContext? project = null)
    {
        File = file;
        Map = map;
        // Map inherits: referenced groups (in order) → default. Computed lazily so it
        // reflects the current GroupSettings list at Reset time rather than at VM construction.
        Properties = new PropertySetViewModel(map.Properties, PropertyScope.Map, project,
            inheritanceChain: () => BuildMapChain(file, map, project));
    }

    internal static List<PropertySet> BuildMapChain(MapConfigFile file, MapEntryModel map, ProjectContext? project)
    {
        var list = new List<PropertySet>();
        if (map.Properties.HasGroupSettings && project is not null)
        {
            foreach (var gn in map.Properties.GroupSettings)
            {
                var g = FindGroup(gn, project);
                if (g is not null) list.Add(g.Properties);
            }
        }
        if (file.DefaultSettings is not null) list.Add(file.DefaultSettings);
        return list;
    }

    private static GroupEntryModel? FindGroup(string name, ProjectContext project)
    {
        foreach (var f in project.Files)
            foreach (var g in f.Groups)
                if (string.Equals(g.GroupName, name, System.StringComparison.OrdinalIgnoreCase))
                    return g;
        return null;
    }

    [RelayCommand]
    private void AddDaySettings()
    {
        var name = GenerateUniqueName();
        Map.DaySettings.Add(new DaySettingsOverrideModel { Name = name });
    }

    [RelayCommand]
    private void RemoveDaySettings(DaySettingsOverrideModel ov) => Map.DaySettings.Remove(ov);

    private string GenerateUniqueName()
    {
        var baseName = "NewOverride";
        var i = 0;
        while (true)
        {
            var candidate = i == 0 ? baseName : $"{baseName}{i}";
            bool exists = false;
            foreach (var ov in Map.DaySettings)
            {
                if (ov.Name == candidate) { exists = true; break; }
            }
            if (!exists)
                return candidate;
            i++;
        }
    }
}

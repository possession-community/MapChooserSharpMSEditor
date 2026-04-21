// LEGACY — remove when MCS migration completes
using System.Collections.Generic;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

public sealed class LegacyMapEditorViewModel : ViewModelBase
{
    public LegacyMapConfigFile File { get; }
    public LegacyMapEntry Map { get; }
    public LegacyPropertySetViewModel Properties { get; }

    public LegacyMapEditorViewModel(LegacyMapConfigFile file, LegacyMapEntry map, LegacyProjectContext? project = null)
    {
        File = file;
        Map = map;
        Properties = new LegacyPropertySetViewModel(map.Properties, LegacyPropertyScope.Map, project,
            inheritanceChain: () => BuildMapChain(file, map, project));
    }

    internal static List<LegacyPropertySet> BuildMapChain(LegacyMapConfigFile file, LegacyMapEntry map, LegacyProjectContext? project)
    {
        var list = new List<LegacyPropertySet>();
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

    private static LegacyGroupEntry? FindGroup(string name, LegacyProjectContext project)
    {
        foreach (var f in project.Files)
            foreach (var g in f.Groups)
                if (string.Equals(g.GroupName, name, System.StringComparison.OrdinalIgnoreCase))
                    return g;
        return null;
    }
}

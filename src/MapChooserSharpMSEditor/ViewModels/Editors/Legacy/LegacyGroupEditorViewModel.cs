// LEGACY — remove when MCS migration completes
using System.Collections.Generic;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.ViewModels.Editors.Legacy;

public sealed class LegacyGroupEditorViewModel : ViewModelBase
{
    public LegacyMapConfigFile File { get; }
    public LegacyGroupEntry Group { get; }
    public LegacyPropertySetViewModel Properties { get; }

    public LegacyGroupEditorViewModel(LegacyMapConfigFile file, LegacyGroupEntry group, LegacyProjectContext? project = null)
    {
        File = file;
        Group = group;
        Properties = new LegacyPropertySetViewModel(group.Properties, LegacyPropertyScope.Group, project,
            inheritanceChain: () =>
            {
                var list = new List<LegacyPropertySet>();
                if (file.DefaultSettings is not null) list.Add(file.DefaultSettings);
                return list;
            });
    }
}

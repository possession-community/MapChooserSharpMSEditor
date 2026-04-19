using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

public sealed partial class GroupEditorViewModel : ViewModelBase
{
    public MapConfigFile File { get; }
    public GroupEntryModel Group { get; }
    public PropertySetViewModel Properties { get; }

    public GroupEditorViewModel(MapConfigFile file, GroupEntryModel group, ProjectContext? project = null)
    {
        File = file;
        Group = group;
        // Group inherits from Default only.
        Properties = new PropertySetViewModel(group.Properties, PropertyScope.Group, project,
            inheritanceChain: () =>
            {
                var list = new List<PropertySet>();
                if (file.DefaultSettings is not null) list.Add(file.DefaultSettings);
                return list;
            });
    }

    [RelayCommand]
    private void AddDaySettings()
    {
        var baseName = "NewOverride";
        var i = 0;
        while (true)
        {
            var candidate = i == 0 ? baseName : $"{baseName}{i}";
            bool exists = false;
            foreach (var ov in Group.DaySettings)
            {
                if (ov.Name == candidate) { exists = true; break; }
            }
            if (!exists)
            {
                Group.DaySettings.Add(new DaySettingsOverrideModel { Name = candidate });
                return;
            }
            i++;
        }
    }

    [RelayCommand]
    private void RemoveDaySettings(DaySettingsOverrideModel ov) => Group.DaySettings.Remove(ov);
}

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
        Properties = new PropertySetViewModel(map.Properties, project);
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

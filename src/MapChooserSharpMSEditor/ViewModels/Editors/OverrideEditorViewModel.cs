using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

public sealed class OverrideEditorViewModel : ViewModelBase
{
    public MapConfigFile File { get; }
    public DaySettingsOverrideModel Override { get; }
    public PropertySetViewModel Properties { get; }

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
        Properties = new PropertySetViewModel(ov.Properties, project);
        ParentDisplay = parent switch
        {
            MapEntryModel m => $"Map: {m.MapName}",
            GroupEntryModel g => $"Group: {g.GroupName}",
            _ => "",
        };
    }
}

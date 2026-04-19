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
        // Override inherits the scope of its parent so the UI hides the same rows as the
        // parent editor; a GroupDaySettings override can't set MapNameAlias any more than
        // the group itself can.
        var scope = parent switch
        {
            MapEntryModel => PropertyScope.Map,
            GroupEntryModel => PropertyScope.Group,
            _ => PropertyScope.Default,
        };
        Properties = new PropertySetViewModel(ov.Properties, scope, project);
        ParentDisplay = parent switch
        {
            MapEntryModel m => $"Map: {m.MapName}",
            GroupEntryModel g => $"Group: {g.GroupName}",
            _ => "",
        };
    }
}

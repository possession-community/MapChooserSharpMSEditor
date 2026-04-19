using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

public sealed class DefaultSettingsViewModel : ViewModelBase
{
    public MapConfigFile File { get; }
    public PropertySetViewModel Properties { get; }

    public DefaultSettingsViewModel(MapConfigFile file, ProjectContext? project = null)
    {
        File = file;
        file.DefaultSettings ??= new PropertySet();
        // Default has no further inheritance — Reset falls back to type-level defaults.
        Properties = new PropertySetViewModel(file.DefaultSettings, PropertyScope.Default, project);
    }
}

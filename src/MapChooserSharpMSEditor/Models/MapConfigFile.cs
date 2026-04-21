using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models;

public partial class MapConfigFile : ObservableObject
{
    /// <summary>
    /// Absolute path to the .toml file. Null for an unsaved in-memory file.
    /// </summary>
    [ObservableProperty] private string? _filePath;

    /// <summary>
    /// Display name (typically the file name or a placeholder).
    /// </summary>
    [ObservableProperty] private string _displayName = "untitled.toml";

    [ObservableProperty] private bool _isDirty;

    /// <summary>
    /// The [MapChooserSharpSettings.Default] section, if present in this file.
    /// Only one Default section across the whole project is meaningful, but we
    /// track it per-file so editing the file that owns it keeps edits local.
    /// </summary>
    [ObservableProperty] private PropertySet? _defaultSettings;

    public ObservableCollection<GroupEntryModel> Groups { get; } = new();
    public ObservableCollection<MapEntryModel> Maps { get; } = new();

    // Used by CollectionJoinConverter so workspace warning banners (e.g. "duplicate section
    // Foo → A.toml, B.toml") print filenames rather than the type name.
    public override string ToString() => DisplayName;
}

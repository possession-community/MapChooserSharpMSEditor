using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models;

public partial class MapEntryModel : ObservableObject
{
    /// <summary>
    /// Map key used as the TOML section name ([mapname]).
    /// </summary>
    [ObservableProperty] private string _mapName = "new_map";

    public PropertySet Properties { get; } = new();

    public ObservableCollection<DaySettingsOverrideModel> DaySettings { get; } = new();
}

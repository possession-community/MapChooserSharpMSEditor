// LEGACY — remove when MCS migration completes
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models.Legacy;

public partial class LegacyMapEntry : ObservableObject
{
    [ObservableProperty] private string _mapName = "new_map";
    public LegacyPropertySet Properties { get; } = new();
}

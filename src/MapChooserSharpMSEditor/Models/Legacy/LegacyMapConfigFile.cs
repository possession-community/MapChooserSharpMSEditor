// LEGACY — remove when MCS migration completes
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models.Legacy;

public partial class LegacyMapConfigFile : ObservableObject
{
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private string _displayName = "untitled.toml";
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private LegacyPropertySet? _defaultSettings;

    public ObservableCollection<LegacyGroupEntry> Groups { get; } = new();
    public ObservableCollection<LegacyMapEntry> Maps { get; } = new();

    public override string ToString() => DisplayName;
}

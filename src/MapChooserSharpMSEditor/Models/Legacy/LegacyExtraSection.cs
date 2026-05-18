// LEGACY — remove when MCS migration completes
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models.Legacy;

public partial class LegacyExtraKeyValue : ObservableObject
{
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
}

public partial class LegacyExtraSection : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    public ObservableCollection<LegacyExtraKeyValue> Entries { get; } = new();
}

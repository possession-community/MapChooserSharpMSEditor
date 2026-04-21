// LEGACY — remove when MCS migration completes
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models.Legacy;

public partial class LegacyGroupEntry : ObservableObject
{
    [ObservableProperty] private string _groupName = "NewGroup";
    public LegacyPropertySet Properties { get; } = new();
}

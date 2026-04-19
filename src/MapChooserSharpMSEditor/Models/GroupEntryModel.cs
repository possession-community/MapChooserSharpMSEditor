using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models;

public partial class GroupEntryModel : ObservableObject
{
    /// <summary>
    /// Group key used as part of the TOML section name ([MapChooserSharpSettings.Groups.GroupName]).
    /// </summary>
    [ObservableProperty] private string _groupName = "NewGroup";

    public PropertySet Properties { get; } = new();

    public ObservableCollection<DaySettingsOverrideModel> DaySettings { get; } = new();
}

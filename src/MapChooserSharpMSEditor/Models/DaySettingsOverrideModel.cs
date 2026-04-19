using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models;

public partial class DaySettingsOverrideModel : ObservableObject
{
    /// <summary>
    /// Override identifier used as the section name ([key.DaySettings.Name]).
    /// Must be a valid TOML bare-key.
    /// </summary>
    [ObservableProperty] private string _name = "NewOverride";

    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private bool _forceOverride;
    [ObservableProperty] private int _overridePriority;

    public ObservableCollection<DayOfWeek> TargetDays { get; } = new();
    public ObservableCollection<TimeRangeSpec> TargetTimeRanges { get; } = new();

    /// <summary>
    /// Properties that are overridden when this day-settings window is active.
    /// Only properties with <c>HasX = true</c> are written.
    /// </summary>
    public PropertySet Properties { get; } = new();
}

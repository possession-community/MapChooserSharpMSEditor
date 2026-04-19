namespace MapChooserSharpMSEditor.Models;

public enum SearchResultKind
{
    Default,
    Group,
    Map,
    Override,
}

/// <summary>
/// One row in the search suggestions list. <see cref="Target"/> is the concrete model object
/// the sidebar should navigate to on activation (MapConfigFile for Default, GroupEntryModel,
/// MapEntryModel, or DaySettingsOverrideModel).
/// </summary>
public sealed record SearchResult(
    SearchResultKind Kind,
    string Label,
    string FileName,
    MapConfigFile File,
    object Target);

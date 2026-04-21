// LEGACY — remove when MCS migration completes
namespace MapChooserSharpMSEditor.Models.Legacy;

public enum LegacySearchResultKind
{
    Default,
    Group,
    Map,
}

public sealed record LegacySearchResult(
    LegacySearchResultKind Kind,
    string Label,
    string FileName,
    LegacyMapConfigFile File,
    object Target);

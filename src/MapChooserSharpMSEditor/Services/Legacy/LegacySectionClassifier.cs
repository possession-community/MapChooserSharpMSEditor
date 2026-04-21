// LEGACY — remove when MCS migration completes
namespace MapChooserSharpMSEditor.Services.Legacy;

internal enum LegacySectionType
{
    Default,
    GroupSetting,
    GroupExtra,
    MapSetting,
    MapExtra,
}

internal static class LegacySectionClassifier
{
    public const string DefaultKey = "MapChooserSharpSettings.Default";
    public const string GroupsPrefix = "MapChooserSharpSettings.Groups.";
    public const string SettingsPrefix = "MapChooserSharpSettings";

    public static LegacySectionType Classify(string key)
    {
        if (key == DefaultKey)
            return LegacySectionType.Default;

        if (key.StartsWith(GroupsPrefix))
        {
            var subpath = key.Substring(GroupsPrefix.Length);
            return subpath.Contains(".extra.") ? LegacySectionType.GroupExtra : LegacySectionType.GroupSetting;
        }

        return key.Contains(".extra.") ? LegacySectionType.MapExtra : LegacySectionType.MapSetting;
    }

    public static string ExtractGroupName(string key)
    {
        var afterGroups = key.Substring(GroupsPrefix.Length);
        var dotIndex = afterGroups.IndexOf('.');
        return dotIndex >= 0 ? afterGroups.Substring(0, dotIndex) : afterGroups;
    }

    public static string ExtractMapName(string key)
    {
        var dotIndex = key.IndexOf('.');
        return dotIndex >= 0 ? key.Substring(0, dotIndex) : key;
    }

    public static string ExtractExtraSectionName(string key)
    {
        var idx = key.IndexOf(".extra.");
        var after = key.Substring(idx + ".extra.".Length);
        var dotIndex = after.IndexOf('.');
        return dotIndex >= 0 ? after.Substring(0, dotIndex) : after;
    }
}

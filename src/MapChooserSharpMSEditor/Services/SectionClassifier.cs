namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Classifies TOML dotted-key paths into MCS config section types.
/// Mirrors <c>MapChooserSharpMS.Modules.MapConfig.Services.TomlSectionClassifier</c> so
/// the editor consumes exactly the same schema as the server.
/// </summary>
internal enum SectionType
{
    Default,
    GroupSetting,
    GroupExtra,
    GroupDaySetting,
    GroupDaySettingExtra,
    MapSetting,
    MapExtra,
    MapDaySetting,
    MapDaySettingExtra,
}

internal static class SectionClassifier
{
    public const string DefaultKey = "MapChooserSharpSettings.Default";
    public const string GroupsPrefix = "MapChooserSharpSettings.Groups.";
    public const string SettingsPrefix = "MapChooserSharpSettings";

    public static SectionType Classify(string key)
    {
        if (key == DefaultKey)
            return SectionType.Default;

        if (key.StartsWith(GroupsPrefix))
            return ClassifyGroupSubpath(key.Substring(GroupsPrefix.Length));

        return ClassifyMapSubpath(key);
    }

    private static SectionType ClassifyGroupSubpath(string subpath)
    {
        var daySettingsIdx = subpath.IndexOf(".DaySettings.");
        if (daySettingsIdx >= 0)
        {
            var after = subpath.Substring(daySettingsIdx + ".DaySettings.".Length);
            return after.Contains(".extra.") ? SectionType.GroupDaySettingExtra : SectionType.GroupDaySetting;
        }
        if (subpath.Contains(".extra."))
            return SectionType.GroupExtra;
        return SectionType.GroupSetting;
    }

    private static SectionType ClassifyMapSubpath(string key)
    {
        var daySettingsIdx = key.IndexOf(".DaySettings.");
        if (daySettingsIdx >= 0)
        {
            var after = key.Substring(daySettingsIdx + ".DaySettings.".Length);
            return after.Contains(".extra.") ? SectionType.MapDaySettingExtra : SectionType.MapDaySetting;
        }
        if (key.Contains(".extra."))
            return SectionType.MapExtra;
        return SectionType.MapSetting;
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

    public static string ExtractDaySettingsName(string key)
    {
        var idx = key.IndexOf(".DaySettings.");
        var after = key.Substring(idx + ".DaySettings.".Length);
        var dotIndex = after.IndexOf('.');
        return dotIndex >= 0 ? after.Substring(0, dotIndex) : after;
    }

    public static string ExtractExtraSectionName(string key)
    {
        var idx = key.IndexOf(".extra.");
        var after = key.Substring(idx + ".extra.".Length);
        var dotIndex = after.IndexOf('.');
        return dotIndex >= 0 ? after.Substring(0, dotIndex) : after;
    }
}

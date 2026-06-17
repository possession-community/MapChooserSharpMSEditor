using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CsToml;
using CsToml.Extensions;
using CsToml.Values;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Loads a TOML file into a <see cref="MapConfigFile"/> suitable for editing.
/// Unlike the server-side parser, this does *not* merge defaults/groups into maps;
/// each section is preserved as-is so the user can edit and save it unchanged.
/// </summary>
public static class TomlConfigLoader
{
    public static MapConfigFile LoadFile(string path)
    {
        Log.Debug("Loader", $"LoadFile {path}");
        var file = new MapConfigFile
        {
            FilePath = path,
            DisplayName = Path.GetFileName(path),
        };

        var doc = CsTomlFileSerializer.Deserialize<TomlDocument>(path);
        Populate(file, doc);
        Log.Info("Loader",
            $"Loaded {file.DisplayName}: groups={file.Groups.Count}, maps={file.Maps.Count}, hasDefault={file.DefaultSettings is not null}");
        return file;
    }

    internal static void Populate(MapConfigFile file, TomlDocument doc)
    {
        var sections = new List<(string FullKey, SectionType Type, TomlDocumentNode Node)>();
        CollectSections(doc.RootNode, "", sections);

        // Pass 1: Default, plain Groups, plain Maps
        var groupLookup = new Dictionary<string, GroupEntryModel>(StringComparer.OrdinalIgnoreCase);
        var mapLookup = new Dictionary<string, MapEntryModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, type, node) in sections)
        {
            switch (type)
            {
                case SectionType.Default:
                    file.DefaultSettings ??= new PropertySet();
                    ReadProperties(node, file.DefaultSettings);
                    break;

                case SectionType.GroupSetting:
                {
                    var name = SectionClassifier.ExtractGroupName(key);
                    if (!groupLookup.TryGetValue(name, out var g))
                    {
                        g = new GroupEntryModel { GroupName = name };
                        groupLookup[name] = g;
                        file.Groups.Add(g);
                    }
                    ReadProperties(node, g.Properties);
                    break;
                }

                case SectionType.MapSetting:
                {
                    var name = SectionClassifier.ExtractMapName(key);
                    if (!mapLookup.TryGetValue(name, out var m))
                    {
                        m = new MapEntryModel { MapName = name };
                        mapLookup[name] = m;
                        file.Maps.Add(m);
                    }
                    ReadProperties(node, m.Properties);
                    break;
                }
            }
        }

        // Pass 2: extras (may reference a group/map created above; if the parent
        // section was missing we synthesize an empty entry so edits round-trip)
        foreach (var (key, type, node) in sections)
        {
            switch (type)
            {
                case SectionType.GroupExtra:
                {
                    var name = SectionClassifier.ExtractGroupName(key);
                    if (!groupLookup.TryGetValue(name, out var g))
                    {
                        g = new GroupEntryModel { GroupName = name };
                        groupLookup[name] = g;
                        file.Groups.Add(g);
                    }
                    ReadExtraSection(g.Properties, SectionClassifier.ExtractExtraSectionName(key), node);
                    break;
                }
                case SectionType.MapExtra:
                {
                    var name = SectionClassifier.ExtractMapName(key);
                    if (!mapLookup.TryGetValue(name, out var m))
                    {
                        m = new MapEntryModel { MapName = name };
                        mapLookup[name] = m;
                        file.Maps.Add(m);
                    }
                    ReadExtraSection(m.Properties, SectionClassifier.ExtractExtraSectionName(key), node);
                    break;
                }
            }
        }

        // Pass 3: DaySettings
        var groupDayLookup = new Dictionary<(string Group, string Name), DaySettingsOverrideModel>();
        var mapDayLookup = new Dictionary<(string Map, string Name), DaySettingsOverrideModel>();

        foreach (var (key, type, node) in sections)
        {
            switch (type)
            {
                case SectionType.GroupDaySetting:
                {
                    var groupName = SectionClassifier.ExtractGroupName(key);
                    var overrideName = SectionClassifier.ExtractDaySettingsName(key);
                    if (!groupLookup.TryGetValue(groupName, out var g))
                    {
                        g = new GroupEntryModel { GroupName = groupName };
                        groupLookup[groupName] = g;
                        file.Groups.Add(g);
                    }
                    var ov = new DaySettingsOverrideModel { Name = overrideName };
                    ReadOverrideProperties(node, ov);
                    g.DaySettings.Add(ov);
                    groupDayLookup[(groupName, overrideName)] = ov;
                    break;
                }
                case SectionType.MapDaySetting:
                {
                    var mapName = SectionClassifier.ExtractMapName(key);
                    var overrideName = SectionClassifier.ExtractDaySettingsName(key);
                    if (!mapLookup.TryGetValue(mapName, out var m))
                    {
                        m = new MapEntryModel { MapName = mapName };
                        mapLookup[mapName] = m;
                        file.Maps.Add(m);
                    }
                    var ov = new DaySettingsOverrideModel { Name = overrideName };
                    ReadOverrideProperties(node, ov);
                    m.DaySettings.Add(ov);
                    mapDayLookup[(mapName, overrideName)] = ov;
                    break;
                }
            }
        }

        // Pass 4: DaySettings extras
        foreach (var (key, type, node) in sections)
        {
            switch (type)
            {
                case SectionType.GroupDaySettingExtra:
                {
                    var groupName = SectionClassifier.ExtractGroupName(key);
                    var overrideName = SectionClassifier.ExtractDaySettingsName(key);
                    if (groupDayLookup.TryGetValue((groupName, overrideName), out var ov))
                        ReadExtraSection(ov.Properties, SectionClassifier.ExtractExtraSectionName(key), node);
                    break;
                }
                case SectionType.MapDaySettingExtra:
                {
                    var mapName = SectionClassifier.ExtractMapName(key);
                    var overrideName = SectionClassifier.ExtractDaySettingsName(key);
                    if (mapDayLookup.TryGetValue((mapName, overrideName), out var ov))
                        ReadExtraSection(ov.Properties, SectionClassifier.ExtractExtraSectionName(key), node);
                    break;
                }
            }
        }
    }

    private static void CollectSections(
        TomlDocumentNode node, string prefix,
        List<(string FullKey, SectionType Type, TomlDocumentNode Node)> result)
    {
        foreach (var kv in node.GetNodeEnumerator())
        {
            var keyName = kv.Key.GetString();
            var fullKey = string.IsNullOrEmpty(prefix) ? keyName : $"{prefix}.{keyName}";
            var childNode = kv.Value;

            bool isLeaf;
            try { isLeaf = childNode.HasValueOnly; }
            catch { isLeaf = false; }

            if (isLeaf)
                continue;

            if (!IsIntermediateContainer(fullKey))
            {
                var type = SectionClassifier.Classify(fullKey);
                result.Add((fullKey, type, childNode));
            }

            CollectSections(childNode, fullKey, result);
        }
    }

    private static bool IsIntermediateContainer(string fullKey)
    {
        if (fullKey == SectionClassifier.SettingsPrefix || fullKey == "MapChooserSharpSettings.Groups")
            return true;

        var lastDot = fullKey.LastIndexOf('.');
        if (lastDot >= 0)
        {
            var last = fullKey.Substring(lastDot + 1);
            if (last == "extra" || last == "DaySettings")
                return true;
        }
        return false;
    }

    private static void ReadProperties(TomlDocumentNode node, PropertySet props)
    {
        foreach (var kv in node.GetNodeEnumerator())
        {
            var key = kv.Key.GetString();
            if (key == "extra" || key == "DaySettings")
                continue;
            ApplyProperty(props, key, kv.Value);
        }
    }

    private static void ReadOverrideProperties(TomlDocumentNode node, DaySettingsOverrideModel ov)
    {
        foreach (var kv in node.GetNodeEnumerator())
        {
            var key = kv.Key.GetString();
            if (key == "extra" || key == "DaySettings")
                continue;

            switch (key)
            {
                case "Enabled":
                    if (kv.Value.TryGetBool(out var en)) ov.Enabled = en;
                    continue;
                case "ForceOverride":
                    if (kv.Value.TryGetBool(out var fo)) ov.ForceOverride = fo;
                    continue;
                case "OverridePriority":
                    if (kv.Value.TryGetInt64(out var p)) ov.OverridePriority = (int)p;
                    continue;
                case "TargetDays":
                    ov.TargetDays.Clear();
                    foreach (var d in ReadDayArray(kv.Value))
                        ov.TargetDays.Add(d);
                    continue;
                case "TargetTimeRanges":
                    ov.TargetTimeRanges.Clear();
                    foreach (var r in ReadTimeRangeArray(kv.Value))
                        ov.TargetTimeRanges.Add(r);
                    continue;
            }

            ApplyProperty(ov.Properties, key, kv.Value);
        }
    }

    private static void ApplyProperty(PropertySet p, string key, TomlDocumentNode v)
    {
        switch (key)
        {
            case "MapNameAlias":
                if (v.TryGetString(out var alias)) { p.HasMapNameAlias = true; p.MapNameAlias = alias; }
                break;
            case "MapDescription":
                if (v.TryGetString(out var desc)) { p.HasMapDescription = true; p.MapDescription = desc; }
                break;
            case "WorkshopId":
                if (v.TryGetInt64(out var wid)) { p.HasWorkshopId = true; p.WorkshopId = wid; }
                break;
            case "GroupSettings":
                p.HasGroupSettings = true;
                p.GroupSettings.Clear();
                foreach (var s in ReadStringArray(v)) p.GroupSettings.Add(s);
                break;
            case "SearchTags":
                p.HasSearchTags = true;
                p.SearchTags.Clear();
                foreach (var s in ReadStringArray(v)) p.SearchTags.Add(s);
                break;
            case "CooldownOverride":
                if (v.TryGetInt64(out var co)) { p.HasCooldownOverride = true; p.CooldownOverride = (int)co; }
                break;
            case "ShortGroupName":
                if (v.TryGetString(out var sgn)) { p.HasShortGroupName = true; p.ShortGroupName = sgn; }
                break;
            case "NominationLimit":
                if (v.TryGetInt64(out var nl)) { p.HasNominationLimit = true; p.NominationLimit = Math.Max(0, (int)nl); }
                break;
            case "IsDisabled":
                if (v.TryGetBool(out var dis)) { p.HasIsDisabled = true; p.IsDisabled = dis; }
                break;
            case "MaxExtends":
                if (v.TryGetInt64(out var me)) { p.HasMaxExtends = true; p.MaxExtends = (int)me; }
                break;
            case "MaxExtCommandUses":
                if (v.TryGetInt64(out var mc)) { p.HasMaxExtCommandUses = true; p.MaxExtCommandUses = (int)mc; }
                break;
            case "ExtendTimePerExtends":
                if (v.TryGetInt64(out var et)) { p.HasExtendTimePerExtends = true; p.ExtendTimePerExtends = (int)et; }
                break;
            case "MapTime":
                if (v.TryGetInt64(out var mt)) { p.HasMapTime = true; p.MapTime = (int)mt; }
                break;
            case "ExtendRoundsPerExtends":
                if (v.TryGetInt64(out var er)) { p.HasExtendRoundsPerExtends = true; p.ExtendRoundsPerExtends = (int)er; }
                break;
            case "MapRounds":
                if (v.TryGetInt64(out var mr)) { p.HasMapRounds = true; p.MapRounds = (int)mr; }
                break;
            case "OnlyNomination":
                if (v.TryGetBool(out var on)) { p.HasOnlyNomination = true; p.OnlyNomination = on; }
                break;
            case "MapSelectionWeight":
                if (v.TryGetInt64(out var msw)) { p.HasMapSelectionWeight = true; p.MapSelectionWeight = (int)msw; }
                break;
            case "MaxPlayers":
                if (v.TryGetInt64(out var mp)) { p.HasMaxPlayers = true; p.MaxPlayers = (int)mp; }
                break;
            case "MinPlayers":
                if (v.TryGetInt64(out var mnp)) { p.HasMinPlayers = true; p.MinPlayers = (int)mnp; }
                break;
            case "ProhibitAdminNomination":
                if (v.TryGetBool(out var pan)) { p.HasProhibitAdminNomination = true; p.ProhibitAdminNomination = pan; }
                break;
            case "RestrictToAllowedUsersOnly":
                if (v.TryGetBool(out var rta)) { p.HasRestrictToAllowedUsersOnly = true; p.RestrictToAllowedUsersOnly = rta; }
                break;
            case "DaysAllowed":
                p.HasDaysAllowed = true;
                p.DaysAllowed.Clear();
                foreach (var d in ReadDayArray(v)) p.DaysAllowed.Add(d);
                break;
            case "AllowedTimeRanges":
                p.HasAllowedTimeRanges = true;
                p.AllowedTimeRanges.Clear();
                foreach (var r in ReadTimeRangeArray(v)) p.AllowedTimeRanges.Add(r);
                break;
            case "Cooldown":
                if (v.TryGetInt64(out var cd)) { p.HasCooldown = true; p.Cooldown = (int)cd; }
                break;
            case "CooldownDateTime":
                if (v.TryGetString(out var cdt)) { p.HasCooldownDateTime = true; p.CooldownDateTime = cdt; }
                break;
            case "NominationCooldown":
                if (v.TryGetInt64(out var ncd)) { p.HasNominationCooldown = true; p.NominationCooldown = (int)ncd; }
                break;
            case "NominationCooldownDateTime":
                if (v.TryGetString(out var ncdt)) { p.HasNominationCooldownDateTime = true; p.NominationCooldownDateTime = ncdt; }
                break;
        }
    }

    private static void ReadExtraSection(PropertySet props, string sectionName, TomlDocumentNode node)
    {
        var section = new ExtraSection { Name = sectionName };
        foreach (var kv in node.GetNodeEnumerator())
        {
            var key = kv.Key.GetString();
            var val = kv.Value;

            // CsToml's TryGetX methods coerce across types (e.g. TryGetString returns "100"
            // for an integer). Use the actual ValueType to preserve TOML typing on roundtrip.
            switch (val.ValueType)
            {
                case TomlValueType.Boolean when val.TryGetBool(out var b):
                    section.Entries.Add(new ExtraKeyValue { Key = key, Value = b ? "true" : "false", Kind = ExtraValueKind.Boolean });
                    break;
                case TomlValueType.Integer when val.TryGetInt64(out var i):
                    section.Entries.Add(new ExtraKeyValue { Key = key, Value = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Kind = ExtraValueKind.Integer });
                    break;
                case TomlValueType.Float when val.TryGetDouble(out var d):
                    section.Entries.Add(new ExtraKeyValue { Key = key, Value = d.ToString(System.Globalization.CultureInfo.InvariantCulture), Kind = ExtraValueKind.Float });
                    break;
                case TomlValueType.String when val.TryGetString(out var s):
                    section.Entries.Add(new ExtraKeyValue { Key = key, Value = s, Kind = ExtraValueKind.String });
                    break;
            }
        }
        props.Extras.Add(section);
    }

    private static List<string> ReadStringArray(TomlDocumentNode node)
    {
        var result = new List<string>();
        try
        {
            var array = node.GetArray();
            foreach (var item in array)
                if (item.TryGetString(out var s))
                    result.Add(s);
        }
        catch { }
        return result;
    }

    private static List<DayOfWeek> ReadDayArray(TomlDocumentNode node)
    {
        var result = new List<DayOfWeek>();
        try
        {
            var array = node.GetArray();
            foreach (var item in array)
                if (item.TryGetString(out var s) && Enum.TryParse<DayOfWeek>(s, ignoreCase: true, out var d))
                    result.Add(d);
        }
        catch { }
        return result;
    }

    private static List<TimeRangeSpec> ReadTimeRangeArray(TomlDocumentNode node)
    {
        var result = new List<TimeRangeSpec>();
        try
        {
            var array = node.GetArray();
            foreach (var item in array)
                if (item.TryGetString(out var s) && TimeRangeSpec.TryParse(s, out var r) && r is not null)
                    result.Add(r);
        }
        catch { }
        return result;
    }
}

// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CsToml;
using CsToml.Extensions;
using CsToml.Values;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.Services.Legacy;

public static class LegacyConfigLoader
{
    public static LegacyMapConfigFile LoadFile(string path)
    {
        Log.Debug("LegacyLoader", $"LoadFile {path}");
        var file = new LegacyMapConfigFile
        {
            FilePath = path,
            DisplayName = Path.GetFileName(path),
        };

        var doc = CsTomlFileSerializer.Deserialize<TomlDocument>(path);
        Populate(file, doc);
        Log.Info("LegacyLoader",
            $"Loaded {file.DisplayName}: groups={file.Groups.Count}, maps={file.Maps.Count}, hasDefault={file.DefaultSettings is not null}");
        return file;
    }

    /// <summary>
    /// Parse TOML content directly from a string. Used when the content comes from
    /// somewhere other than a disk path — e.g. <c>git show branch:path</c> piping a
    /// historical revision into the branch-diff tool without materializing a temp file.
    /// </summary>
    public static LegacyMapConfigFile LoadFromText(string content, string displayName, string? originalPath = null)
    {
        var file = new LegacyMapConfigFile
        {
            FilePath = originalPath,
            DisplayName = displayName,
        };
        var bytes = Encoding.UTF8.GetBytes(content);
        var doc = CsTomlSerializer.Deserialize<TomlDocument>(bytes);
        Populate(file, doc);
        return file;
    }

    internal static void Populate(LegacyMapConfigFile file, TomlDocument doc)
    {
        var sections = new List<(string FullKey, LegacySectionType Type, TomlDocumentNode Node)>();
        CollectSections(doc.RootNode, "", sections);

        var groupLookup = new Dictionary<string, LegacyGroupEntry>(StringComparer.OrdinalIgnoreCase);
        var mapLookup = new Dictionary<string, LegacyMapEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, type, node) in sections)
        {
            switch (type)
            {
                case LegacySectionType.Default:
                    file.DefaultSettings ??= new LegacyPropertySet();
                    ReadProperties(node, file.DefaultSettings);
                    break;
                case LegacySectionType.GroupSetting:
                {
                    var name = LegacySectionClassifier.ExtractGroupName(key);
                    if (!groupLookup.TryGetValue(name, out var g))
                    {
                        g = new LegacyGroupEntry { GroupName = name };
                        groupLookup[name] = g;
                        file.Groups.Add(g);
                    }
                    ReadProperties(node, g.Properties);
                    break;
                }
                case LegacySectionType.MapSetting:
                {
                    var name = LegacySectionClassifier.ExtractMapName(key);
                    if (!mapLookup.TryGetValue(name, out var m))
                    {
                        m = new LegacyMapEntry { MapName = name };
                        mapLookup[name] = m;
                        file.Maps.Add(m);
                    }
                    ReadProperties(node, m.Properties);
                    break;
                }
            }
        }

        // Pass 2: extras (synthesize parent if missing — same defensive policy as Current loader)
        foreach (var (key, type, node) in sections)
        {
            switch (type)
            {
                case LegacySectionType.GroupExtra:
                {
                    var name = LegacySectionClassifier.ExtractGroupName(key);
                    if (!groupLookup.TryGetValue(name, out var g))
                    {
                        g = new LegacyGroupEntry { GroupName = name };
                        groupLookup[name] = g;
                        file.Groups.Add(g);
                    }
                    ReadExtraSection(g.Properties, LegacySectionClassifier.ExtractExtraSectionName(key), node);
                    break;
                }
                case LegacySectionType.MapExtra:
                {
                    var name = LegacySectionClassifier.ExtractMapName(key);
                    if (!mapLookup.TryGetValue(name, out var m))
                    {
                        m = new LegacyMapEntry { MapName = name };
                        mapLookup[name] = m;
                        file.Maps.Add(m);
                    }
                    ReadExtraSection(m.Properties, LegacySectionClassifier.ExtractExtraSectionName(key), node);
                    break;
                }
            }
        }
    }

    private static void CollectSections(
        TomlDocumentNode node, string prefix,
        List<(string FullKey, LegacySectionType Type, TomlDocumentNode Node)> result)
    {
        foreach (var kv in node.GetNodeEnumerator())
        {
            var keyName = kv.Key.GetString();
            var fullKey = string.IsNullOrEmpty(prefix) ? keyName : $"{prefix}.{keyName}";
            var childNode = kv.Value;

            bool isLeaf;
            try { isLeaf = childNode.HasValueOnly; }
            catch { isLeaf = false; }

            if (isLeaf) continue;

            if (!IsIntermediateContainer(fullKey))
            {
                var type = LegacySectionClassifier.Classify(fullKey);
                result.Add((fullKey, type, childNode));
            }

            CollectSections(childNode, fullKey, result);
        }
    }

    private static bool IsIntermediateContainer(string fullKey)
    {
        if (fullKey == LegacySectionClassifier.SettingsPrefix || fullKey == "MapChooserSharpSettings.Groups")
            return true;
        var lastDot = fullKey.LastIndexOf('.');
        if (lastDot >= 0 && fullKey.Substring(lastDot + 1) == "extra")
            return true;
        return false;
    }

    private static void ReadProperties(TomlDocumentNode node, LegacyPropertySet props)
    {
        foreach (var kv in node.GetNodeEnumerator())
        {
            var key = kv.Key.GetString();
            if (key == "extra") continue;
            ApplyProperty(props, key, kv.Value);
        }
    }

    private static void ApplyProperty(LegacyPropertySet p, string key, TomlDocumentNode v)
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
                if (v.TryGetBool(out var rao)) { p.HasRestrictToAllowedUsersOnly = true; p.RestrictToAllowedUsersOnly = rao; }
                break;
            case "RequiredPermissions":
                p.HasRequiredPermissions = true;
                p.RequiredPermissions.Clear();
                foreach (var s in ReadStringArray(v)) p.RequiredPermissions.Add(s);
                break;
            case "AllowedSteamIds":
                p.HasAllowedSteamIds = true;
                p.AllowedSteamIds.Clear();
                foreach (var u in ReadUlongArray(v)) p.AllowedSteamIds.Add(u);
                break;
            case "DisallowedSteamIds":
                p.HasDisallowedSteamIds = true;
                p.DisallowedSteamIds.Clear();
                foreach (var u in ReadUlongArray(v)) p.DisallowedSteamIds.Add(u);
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
            case "NominationCost":
                if (v.TryGetInt64(out var nc)) { p.HasNominationCost = true; p.NominationCost = (int)nc; }
                break;
            case "NominationSpecificCooldown":
                if (v.TryGetInt64(out var nsc)) { p.HasNominationSpecificCooldown = true; p.NominationSpecificCooldown = (int)nsc; }
                break;
        }
    }

    private static void ReadExtraSection(LegacyPropertySet props, string sectionName, TomlDocumentNode node)
    {
        var section = new LegacyExtraSection { Name = sectionName };
        foreach (var kv in node.GetNodeEnumerator())
        {
            var key = kv.Key.GetString();
            // Legacy ExtraConfiguration is Dictionary<string, Dictionary<string, string>>
            // — coerce every value to string regardless of source TOML type.
            if (kv.Value.TryGetString(out var s))
                section.Entries.Add(new LegacyExtraKeyValue { Key = key, Value = s });
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

    private static List<ulong> ReadUlongArray(TomlDocumentNode node)
    {
        var result = new List<ulong>();
        try
        {
            var array = node.GetArray();
            foreach (var item in array)
            {
                if (item.TryGetInt64(out var i) && i >= 0)
                    result.Add((ulong)i);
                else if (item.TryGetString(out var s) && ulong.TryParse(s, out var u))
                    result.Add(u);
            }
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

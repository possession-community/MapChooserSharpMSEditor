// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.Services.Legacy;

/// <summary>
/// Legacy effective-values resolver. Mirrors PropertyResolver but for the simpler Legacy
/// schema: no DaySettings overrides, no CooldownOverride (Cooldown on a group simply
/// applies to its referenced maps), Extras are flat string-typed entries.
///
/// Merge order (highest priority first): own → referenced groups (first wins) → default.
/// </summary>
public static class LegacyPropertyResolver
{
    public record ResolvedRow(string Property, string DisplayValue, string Source)
    {
        public string Label => Localization.Get("Prop." + Property,
            Localization.Get("LegacyProp." + Property, Property));
    }

    public record ResolvedExtraEntry(string Key, string Value, string Source);
    public record ResolvedExtraSection(string Name, IReadOnlyList<ResolvedExtraEntry> Entries);

    private static readonly string[] PropertyNames =
    {
        "MapNameAlias", "MapDescription", "WorkshopId", "IsDisabled",
        "MaxExtends", "MaxExtCommandUses", "ExtendTimePerExtends", "MapTime",
        "ExtendRoundsPerExtends", "MapRounds",
        "OnlyNomination", "MaxPlayers", "MinPlayers", "ProhibitAdminNomination",
        "RestrictToAllowedUsersOnly", "RequiredPermissions",
        "AllowedSteamIds", "DisallowedSteamIds",
        "DaysAllowed", "AllowedTimeRanges",
        "Cooldown", "NominationCost", "NominationSpecificCooldown",
    };

    public static List<ResolvedRow> ResolveDefault(LegacyMapConfigFile file)
    {
        var rows = new List<ResolvedRow>(PropertyNames.Length);
        var def = file.DefaultSettings;
        foreach (var name in PropertyNames)
        {
            if (def is not null && HasFlag(def, name))
                rows.Add(new ResolvedRow(name, Format(Get(def, name)), Localization.Get("Source.Default")));
            else
                rows.Add(new ResolvedRow(name, "—", Localization.Get("Source.Unset")));
        }
        return rows;
    }

    public static List<ResolvedRow> ResolveGroup(LegacyGroupEntry group, LegacyMapConfigFile file, LegacyProjectContext project)
    {
        var chain = new List<(LegacyPropertySet Set, string Source)>
        {
            (group.Properties, Localization.Format("Source.Group", group.GroupName)),
        };
        chain.AddRange(BuildDefaultSources(file, project));
        var rows = new List<ResolvedRow>(PropertyNames.Length);
        foreach (var name in PropertyNames)
            rows.Add(ResolveOne(name, chain));
        return rows;
    }

    public static List<ResolvedRow> ResolveMap(LegacyMapEntry map, LegacyMapConfigFile file, LegacyProjectContext project)
    {
        var chain = BuildMapChain(map, file, project);
        var rows = new List<ResolvedRow>(PropertyNames.Length);
        foreach (var name in PropertyNames)
            rows.Add(ResolveOne(name, chain));
        return rows;
    }

    public static List<ResolvedExtraSection> ResolveDefaultExtras(LegacyMapConfigFile file)
    {
        if (file.DefaultSettings is null) return new();
        return ResolveExtras(new[] { (file.DefaultSettings, Localization.Get("Source.Default")) });
    }

    public static List<ResolvedExtraSection> ResolveGroupExtras(LegacyGroupEntry group, LegacyMapConfigFile file, LegacyProjectContext project)
    {
        var chain = new[] { (group.Properties, Localization.Format("Source.Group", group.GroupName)) }
            .Concat(BuildDefaultSources(file, project));
        return ResolveExtras(chain);
    }

    public static List<ResolvedExtraSection> ResolveMapExtras(LegacyMapEntry map, LegacyMapConfigFile file, LegacyProjectContext project)
        => ResolveExtras(BuildMapChain(map, file, project));

    private static List<(LegacyPropertySet Set, string Source)> BuildMapChain(
        LegacyMapEntry map, LegacyMapConfigFile file, LegacyProjectContext project)
    {
        var chain = new List<(LegacyPropertySet, string)>
        {
            (map.Properties, Localization.Format("Source.Map", map.MapName)),
        };
        if (map.Properties.HasGroupSettings)
        {
            foreach (var groupName in map.Properties.GroupSettings)
            {
                var g = FindGroup(groupName, project);
                if (g is not null)
                    chain.Add((g.Properties, Localization.Format("Source.Group", g.GroupName)));
            }
        }
        chain.AddRange(BuildDefaultSources(file, project));
        return chain;
    }

    private static List<(LegacyPropertySet Set, string Source)> BuildDefaultSources(LegacyMapConfigFile file, LegacyProjectContext project)
    {
        var owner = project.DefaultOwner;
        if (owner is null || owner.DefaultSettings is null)
            return new List<(LegacyPropertySet, string)>();
        var label = owner == file
            ? Localization.Get("Source.Default")
            : Localization.Format("Source.DefaultFromFile", owner.DisplayName);
        return new List<(LegacyPropertySet Set, string Source)>
        {
            (owner.DefaultSettings, label),
        };
    }

    private static LegacyGroupEntry? FindGroup(string name, LegacyProjectContext project)
    {
        foreach (var f in project.Files)
            foreach (var g in f.Groups)
                if (string.Equals(g.GroupName, name, StringComparison.OrdinalIgnoreCase))
                    return g;
        return null;
    }

    private static List<ResolvedExtraSection> ResolveExtras(IEnumerable<(LegacyPropertySet Set, string Source)> chain)
    {
        var byName = new Dictionary<string, Dictionary<string, ResolvedExtraEntry>>(StringComparer.Ordinal);
        var orderedNames = new List<string>();

        foreach (var (set, src) in chain)
        {
            foreach (var section in set.Extras)
            {
                if (string.IsNullOrEmpty(section.Name)) continue;
                if (!byName.TryGetValue(section.Name, out var keyMap))
                {
                    byName[section.Name] = keyMap = new Dictionary<string, ResolvedExtraEntry>(StringComparer.Ordinal);
                    orderedNames.Add(section.Name);
                }
                foreach (var entry in section.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    if (keyMap.ContainsKey(entry.Key)) continue;
                    keyMap[entry.Key] = new ResolvedExtraEntry(entry.Key, entry.Value, src);
                }
            }
        }

        var result = new List<ResolvedExtraSection>(orderedNames.Count);
        foreach (var name in orderedNames)
        {
            var entries = byName[name].Values.ToList();
            if (entries.Count > 0) result.Add(new ResolvedExtraSection(name, entries));
        }
        return result;
    }

    private static ResolvedRow ResolveOne(string property, IEnumerable<(LegacyPropertySet Set, string Source)> chain)
    {
        foreach (var (set, src) in chain)
        {
            if (HasFlag(set, property))
                return new ResolvedRow(property, Format(Get(set, property)), src);
        }
        return new ResolvedRow(property, "—", Localization.Get("Source.Unset"));
    }

    private static bool HasFlag(LegacyPropertySet set, string propertyName)
    {
        var prop = typeof(LegacyPropertySet).GetProperty("Has" + propertyName, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(set) is bool b && b;
    }

    private static object? Get(LegacyPropertySet set, string propertyName)
    {
        var prop = typeof(LegacyPropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(set);
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => "—",
            bool b => b ? "true" : "false",
            string s => string.IsNullOrEmpty(s) ? "\"\"" : s,
            System.Collections.IEnumerable e when value is not string =>
                "[" + string.Join(", ", e.Cast<object?>().Select(Format)) + "]",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "—",
        };
    }
}

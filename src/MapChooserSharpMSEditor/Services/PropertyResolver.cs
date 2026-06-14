using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Computes the effective value the server will use for a given selection, mirroring the
/// merge chain in <c>MapConfigParsingService</c>: default → groups(in order, later wins) → own.
/// DaySettings overrides apply on top of that chain.
/// </summary>
public static class PropertyResolver
{
    public record ResolvedRow(string Property, string DisplayValue, string Source)
    {
        /// <summary>Localized friendly name shown in the Effective Values panel.</summary>
        public string Label => Localization.Get("Prop." + Property, Property);
    }

    /// <summary>One key/value row inside a resolved Extra section. Source is the same
    /// label used on <see cref="ResolvedRow"/> so the user can see whether the value
    /// comes from this entry's own PropertySet or was inherited.</summary>
    public record ResolvedExtraEntry(string Key, string Value, string Kind, string Source);

    /// <summary>Resolved extra-sub-table (e.g. <c>[extra.shop]</c>). Entries are already
    /// deduplicated: higher-priority sources win per (section, key), matching the server's
    /// table-merge behavior.</summary>
    public record ResolvedExtraSection(string Name, IReadOnlyList<ResolvedExtraEntry> Entries);

    /// <summary>Scalar/array properties that appear on <see cref="PropertySet"/>.</summary>
    private static readonly string[] PropertyNames =
    {
        "MapNameAlias", "MapDescription", "WorkshopId", "IsDisabled",
        "MaxExtends", "MaxExtCommandUses", "ExtendTimePerExtends", "MapTime",
        "ExtendRoundsPerExtends", "MapRounds",
        "MapSelectionWeight",
        "OnlyNomination", "MaxPlayers", "MinPlayers", "ProhibitAdminNomination",
        "DaysAllowed", "AllowedTimeRanges",
        "Cooldown", "CooldownDateTime",
        "NominationCooldown", "NominationCooldownDateTime",
    };

    public static List<ResolvedRow> ResolveDefault(MapConfigFile file)
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

    public static List<ResolvedRow> ResolveGroup(GroupEntryModel group, MapConfigFile file, ProjectContext project)
    {
        var sources = BuildDefaultSources(file, project);
        var chain = new[] { (group.Properties, Localization.Format("Source.Group", group.GroupName)) }.Concat(sources);
        var rows = new List<ResolvedRow>(PropertyNames.Length + 1);
        foreach (var name in PropertyNames)
            rows.Add(ResolveOne(name, chain));

        // CooldownOverride is group-only; no inheritance, no Default fallback. Show it as its
        // own row so the user can see what this group pushes into its member maps.
        rows.Add(group.Properties.HasCooldownOverride
            ? new ResolvedRow("CooldownOverride", group.Properties.CooldownOverride.ToString(CultureInfo.InvariantCulture),
                Localization.Format("Source.Group", group.GroupName))
            : new ResolvedRow("CooldownOverride", "—", Localization.Get("Source.Unset")));
        rows.Add(group.Properties.HasShortGroupName
            ? new ResolvedRow("ShortGroupName", group.Properties.ShortGroupName,
                Localization.Format("Source.Group", group.GroupName))
            : new ResolvedRow("ShortGroupName", "—", Localization.Get("Source.Unset")));
        rows.Add(group.Properties.HasNominationLimit
            ? new ResolvedRow("NominationLimit", group.Properties.NominationLimit.ToString(CultureInfo.InvariantCulture),
                Localization.Format("Source.Group", group.GroupName))
            : new ResolvedRow("NominationLimit", "—", Localization.Get("Source.Unset")));
        return rows;
    }

    public static List<ResolvedRow> ResolveMap(MapEntryModel map, MapConfigFile file, ProjectContext project)
    {
        var chain = BuildMapChain(map, file, project);
        var cdOverride = FindGroupCooldownOverride(map, project);
        var rows = new List<ResolvedRow>(PropertyNames.Length);
        foreach (var name in PropertyNames)
            rows.Add(ResolveCooldownAware(name, chain, cdOverride));
        return rows;
    }

    public static List<ResolvedRow> ResolveMapOverride(
        DaySettingsOverrideModel ov, MapEntryModel map, MapConfigFile file, ProjectContext project)
    {
        // The parent map is the "override target" — label it explicitly so users reading
        // the pane can see at a glance that unset rows inherit from that map, not Default.
        var chain = new List<(PropertySet Set, string Source)>
        {
            (ov.Properties, Localization.Format("Source.Override", ov.Name)),
            (map.Properties, Localization.Format("Source.OverrideTargetMap", map.MapName)),
        };
        if (map.Properties.HasGroupSettings)
        {
            foreach (var gn in map.Properties.GroupSettings)
            {
                var g = FindGroup(gn, project);
                if (g is not null) chain.Add((g.Properties, Localization.Format("Source.Group", g.GroupName)));
            }
        }
        chain.AddRange(BuildDefaultSources(file, project));

        var cdOverride = FindGroupCooldownOverride(map, project);
        var rows = new List<ResolvedRow>(PropertyNames.Length);
        foreach (var name in PropertyNames)
            rows.Add(ResolveCooldownAware(name, chain, cdOverride));
        return rows;
    }

    public static List<ResolvedRow> ResolveGroupOverride(
        DaySettingsOverrideModel ov, GroupEntryModel group, MapConfigFile file, ProjectContext project)
    {
        var chain = new List<(PropertySet Set, string Source)>
        {
            (ov.Properties, Localization.Format("Source.Override", ov.Name)),
            (group.Properties, Localization.Format("Source.OverrideTargetGroup", group.GroupName)),
        };
        chain.AddRange(BuildDefaultSources(file, project));
        var rows = new List<ResolvedRow>(PropertyNames.Length);
        foreach (var name in PropertyNames)
            rows.Add(ResolveOne(name, chain));
        return rows;
    }

    /// <summary>
    /// Mirrors the server's "first referenced group with CooldownOverride&gt;0 wins" rule:
    /// such a group's CooldownOverride replaces whatever the normal chain would resolve to
    /// for the <c>Cooldown</c> property.
    /// </summary>
    private static (GroupEntryModel Group, int Value)? FindGroupCooldownOverride(MapEntryModel map, ProjectContext project)
    {
        if (!map.Properties.HasGroupSettings) return null;
        foreach (var gn in map.Properties.GroupSettings)
        {
            var g = FindGroup(gn, project);
            if (g?.Properties.HasCooldownOverride == true && g.Properties.CooldownOverride > 0)
                return (g, g.Properties.CooldownOverride);
        }
        return null;
    }

    private static ResolvedRow ResolveCooldownAware(
        string property, IEnumerable<(PropertySet Set, string Source)> chain,
        (GroupEntryModel Group, int Value)? cdOverride)
    {
        if (property == "Cooldown" && cdOverride is { } ov)
        {
            return new ResolvedRow("Cooldown",
                ov.Value.ToString(CultureInfo.InvariantCulture),
                Localization.Format("Source.GroupCooldownOverride", ov.Group.GroupName));
        }
        return ResolveOne(property, chain);
    }

    /// <summary>
    /// Returns the merge chain for a map (highest priority first): map own → referenced groups
    /// (first wins) → the project's Default section.
    /// </summary>
    private static List<(PropertySet Set, string Source)> BuildMapChain(
        MapEntryModel map, MapConfigFile file, ProjectContext project)
    {
        var chain = new List<(PropertySet, string)> { (map.Properties, Localization.Format("Source.Map", map.MapName)) };

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

    /// <summary>
    /// Returns the single project-wide Default, if one exists. The server's TOML parser
    /// rejects duplicate sections so there can be at most one; the "fall back across
    /// files" scan the earlier version did was never actually reachable for a valid
    /// config. Label it with its host filename when it isn't the caller's own file so
    /// the resolved-panel still tells the user where the value comes from.
    /// </summary>
    private static List<(PropertySet Set, string Source)> BuildDefaultSources(MapConfigFile file, ProjectContext project)
    {
        var owner = project.DefaultOwner;
        if (owner is null || owner.DefaultSettings is null)
            return new List<(PropertySet, string)>();

        var label = owner == file
            ? Localization.Get("Source.Default")
            : Localization.Format("Source.DefaultFromFile", owner.DisplayName);
        return new List<(PropertySet Set, string Source)>
        {
            (owner.DefaultSettings, label),
        };
    }

    private static GroupEntryModel? FindGroup(string name, ProjectContext project)
    {
        foreach (var f in project.Files)
            foreach (var g in f.Groups)
                if (string.Equals(g.GroupName, name, StringComparison.OrdinalIgnoreCase))
                    return g;
        return null;
    }

    // ===== Extras =====

    public static List<ResolvedExtraSection> ResolveDefaultExtras(MapConfigFile file)
    {
        if (file.DefaultSettings is null) return new();
        return ResolveExtras(new[] { (file.DefaultSettings, Localization.Get("Source.Default")) });
    }

    public static List<ResolvedExtraSection> ResolveGroupExtras(GroupEntryModel group, MapConfigFile file, ProjectContext project)
    {
        var chain = new[] { (group.Properties, Localization.Format("Source.Group", group.GroupName)) }
            .Concat(BuildDefaultSources(file, project));
        return ResolveExtras(chain);
    }

    public static List<ResolvedExtraSection> ResolveMapExtras(MapEntryModel map, MapConfigFile file, ProjectContext project)
        => ResolveExtras(BuildMapChain(map, file, project));

    public static List<ResolvedExtraSection> ResolveMapOverrideExtras(
        DaySettingsOverrideModel ov, MapEntryModel map, MapConfigFile file, ProjectContext project)
    {
        var chain = new List<(PropertySet Set, string Source)>
        {
            (ov.Properties, Localization.Format("Source.Override", ov.Name)),
            (map.Properties, Localization.Format("Source.OverrideTargetMap", map.MapName)),
        };
        if (map.Properties.HasGroupSettings)
        {
            foreach (var gn in map.Properties.GroupSettings)
            {
                var g = FindGroup(gn, project);
                if (g is not null) chain.Add((g.Properties, Localization.Format("Source.Group", g.GroupName)));
            }
        }
        chain.AddRange(BuildDefaultSources(file, project));
        return ResolveExtras(chain);
    }

    public static List<ResolvedExtraSection> ResolveGroupOverrideExtras(
        DaySettingsOverrideModel ov, GroupEntryModel group, MapConfigFile file, ProjectContext project)
    {
        var chain = new List<(PropertySet Set, string Source)>
        {
            (ov.Properties, Localization.Format("Source.Override", ov.Name)),
            (group.Properties, Localization.Format("Source.OverrideTargetGroup", group.GroupName)),
        };
        chain.AddRange(BuildDefaultSources(file, project));
        return ResolveExtras(chain);
    }

    /// <summary>
    /// Walks <paramref name="chain"/> highest-priority-first and merges every Extras
    /// sub-section along the way: each (section name, key) pair is kept from the first
    /// source that defines it. Section order is determined by where each section first
    /// appears in the chain so rarely-used inherited sections appear below the caller's
    /// own sections.
    /// </summary>
    private static List<ResolvedExtraSection> ResolveExtras(IEnumerable<(PropertySet Set, string Source)> chain)
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
                    keyMap[entry.Key] = new ResolvedExtraEntry(entry.Key, entry.Value, entry.Kind.ToString(), src);
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

    private static ResolvedRow ResolveOne(string property, IEnumerable<(PropertySet Set, string Source)> chain)
    {
        foreach (var (set, src) in chain)
        {
            if (HasFlag(set, property))
                return new ResolvedRow(property, Format(Get(set, property)), src);
        }
        return new ResolvedRow(property, "—", Localization.Get("Source.Unset"));
    }

    // ===== Reflection helpers =====

    private static bool HasFlag(PropertySet set, string propertyName)
    {
        var prop = typeof(PropertySet).GetProperty("Has" + propertyName, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(set) is bool b && b;
    }

    private static object? Get(PropertySet set, string propertyName)
    {
        var prop = typeof(PropertySet).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
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

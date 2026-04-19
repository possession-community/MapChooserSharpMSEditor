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
    public record ResolvedRow(string Property, string DisplayValue, string Source);

    /// <summary>Scalar/array properties that appear on <see cref="PropertySet"/>.</summary>
    private static readonly string[] PropertyNames =
    {
        "MapNameAlias", "MapDescription", "WorkshopId", "IsDisabled",
        "MaxExtends", "MaxExtCommandUses", "ExtendTimePerExtends", "MapTime",
        "ExtendRoundsPerExtends", "MapRounds",
        "OnlyNomination", "MaxPlayers", "MinPlayers", "ProhibitAdminNomination",
        "DaysAllowed", "AllowedTimeRanges",
        "Cooldown", "CooldownDateTime",
    };

    public static List<ResolvedRow> ResolveDefault(MapConfigFile file)
    {
        var rows = new List<ResolvedRow>(PropertyNames.Length);
        var def = file.DefaultSettings;
        foreach (var name in PropertyNames)
        {
            if (def is not null && HasFlag(def, name))
                rows.Add(new ResolvedRow(name, Format(Get(def, name)), "Default"));
            else
                rows.Add(new ResolvedRow(name, "—", "(unset)"));
        }
        return rows;
    }

    public static List<ResolvedRow> ResolveGroup(GroupEntryModel group, MapConfigFile file, ProjectContext project)
    {
        var sources = BuildDefaultSources(file, project);
        var chain = new[] { (group.Properties, $"Group: {group.GroupName}") }.Concat(sources);
        var rows = new List<ResolvedRow>(PropertyNames.Length + 1);
        foreach (var name in PropertyNames)
            rows.Add(ResolveOne(name, chain));

        // CooldownOverride is group-only; no inheritance, no Default fallback. Show it as its
        // own row so the user can see what this group pushes into its member maps.
        rows.Add(group.Properties.HasCooldownOverride
            ? new ResolvedRow("CooldownOverride", group.Properties.CooldownOverride.ToString(CultureInfo.InvariantCulture), $"Group: {group.GroupName}")
            : new ResolvedRow("CooldownOverride", "—", "(unset)"));
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
            (ov.Properties, $"Override: {ov.Name}"),
            (map.Properties, string.Format(Localization.Get("Source.OverrideTargetMap"), map.MapName)),
        };
        if (map.Properties.HasGroupSettings)
        {
            foreach (var gn in map.Properties.GroupSettings)
            {
                var g = FindGroup(gn, project);
                if (g is not null) chain.Add((g.Properties, $"Group: {g.GroupName}"));
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
            (ov.Properties, $"Override: {ov.Name}"),
            (group.Properties, string.Format(Localization.Get("Source.OverrideTargetGroup"), group.GroupName)),
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
                $"Group: {ov.Group.GroupName} (CooldownOverride)");
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
        var chain = new List<(PropertySet, string)> { (map.Properties, $"Map: {map.MapName}") };

        if (map.Properties.HasGroupSettings)
        {
            foreach (var groupName in map.Properties.GroupSettings)
            {
                var g = FindGroup(groupName, project);
                if (g is not null)
                    chain.Add((g.Properties, $"Group: {g.GroupName}"));
            }
        }

        chain.AddRange(BuildDefaultSources(file, project));
        return chain;
    }

    /// <summary>
    /// Preference: the same file's [Default], then any other file's [Default] (matches the
    /// server parser, which reads the first Default it encounters across the merged documents).
    /// </summary>
    private static List<(PropertySet Set, string Source)> BuildDefaultSources(MapConfigFile file, ProjectContext project)
    {
        var list = new List<(PropertySet, string)>();
        if (file.DefaultSettings is not null)
            list.Add((file.DefaultSettings, "Default"));
        foreach (var f in project.Files)
        {
            if (f != file && f.DefaultSettings is not null)
                list.Add((f.DefaultSettings, $"Default ({f.DisplayName})"));
        }
        return list;
    }

    private static GroupEntryModel? FindGroup(string name, ProjectContext project)
    {
        foreach (var f in project.Files)
            foreach (var g in f.Groups)
                if (string.Equals(g.GroupName, name, StringComparison.OrdinalIgnoreCase))
                    return g;
        return null;
    }

    private static ResolvedRow ResolveOne(string property, IEnumerable<(PropertySet Set, string Source)> chain)
    {
        foreach (var (set, src) in chain)
        {
            if (HasFlag(set, property))
                return new ResolvedRow(property, Format(Get(set, property)), src);
        }
        return new ResolvedRow(property, "—", "(unset)");
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

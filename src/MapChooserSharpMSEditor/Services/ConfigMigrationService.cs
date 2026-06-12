using System;
using System.Collections.Generic;
using System.Linq;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.Services;

public enum MigrationChangeKind
{
    Removed,
    Added,
    CarriedOver,
}

public record MigrationChange(string Property, MigrationChangeKind Kind, string? OldValue = null, string? NewValue = null);

public record MigrationFileResult(
    string SourceDisplayName,
    MapConfigFile ConvertedFile,
    IReadOnlyList<MigrationChange> Changes);

public static class ConfigMigrationService
{
    private static readonly string[] RemovedProperties =
    {
        "RestrictToAllowedUsersOnly",
        "RequiredPermissions",
        "AllowedSteamIds",
        "DisallowedSteamIds",
        "NominationCost",
    };

    private static readonly string[] AddedProperties =
    {
        "CooldownDateTime",
        "CooldownOverride",
        "NominationCooldown",
        "NominationCooldownDateTime",
        "DaySettings",
    };

    public static MigrationFileResult Migrate(LegacyMapConfigFile legacy)
    {
        var changes = new List<MigrationChange>();
        var result = new MapConfigFile
        {
            FilePath = legacy.FilePath,
            DisplayName = legacy.DisplayName,
        };

        if (legacy.DefaultSettings is not null)
        {
            result.DefaultSettings = new PropertySet();
            MigratePropertySet(legacy.DefaultSettings, result.DefaultSettings, changes, "Default");
        }

        foreach (var lg in legacy.Groups)
        {
            var g = new GroupEntryModel { GroupName = lg.GroupName };
            MigratePropertySet(lg.Properties, g.Properties, changes, $"Group:{lg.GroupName}");
            result.Groups.Add(g);
        }

        foreach (var lm in legacy.Maps)
        {
            var m = new MapEntryModel { MapName = lm.MapName };
            MigratePropertySet(lm.Properties, m.Properties, changes, $"Map:{lm.MapName}");
            result.Maps.Add(m);
        }

        // Structural changes: always note the newly available features
        changes.AddRange(AddedProperties.Select(p => new MigrationChange(p, MigrationChangeKind.Added)));

        return new MigrationFileResult(legacy.DisplayName ?? "unknown", result, changes);
    }

    private static void MigratePropertySet(LegacyPropertySet src, PropertySet dst, List<MigrationChange> changes, string context)
    {
        // Set HasX explicitly before assigning values — the MVVM Toolkit setter skips
        // OnChanged when the new value equals the field default (0, false, ""), which
        // would leave HasX=false and silently drop explicitly-set default values.
        if (src.HasMapNameAlias) { dst.HasMapNameAlias = true; dst.MapNameAlias = src.MapNameAlias; CarryOver(changes, "MapNameAlias", src.MapNameAlias, context); }
        if (src.HasMapDescription) { dst.HasMapDescription = true; dst.MapDescription = src.MapDescription; CarryOver(changes, "MapDescription", src.MapDescription, context); }
        if (src.HasWorkshopId) { dst.HasWorkshopId = true; dst.WorkshopId = src.WorkshopId; CarryOver(changes, "WorkshopId", src.WorkshopId.ToString(), context); }
        if (src.HasIsDisabled) { dst.HasIsDisabled = true; dst.IsDisabled = src.IsDisabled; CarryOver(changes, "IsDisabled", src.IsDisabled.ToString(), context); }

        if (src.HasGroupSettings)
        {
            dst.HasGroupSettings = true;
            foreach (var g in src.GroupSettings)
                dst.GroupSettings.Add(g);
            CarryOver(changes, "GroupSettings", string.Join(", ", src.GroupSettings), context);
        }

        if (src.HasMaxExtends) { dst.HasMaxExtends = true; dst.MaxExtends = src.MaxExtends; CarryOver(changes, "MaxExtends", src.MaxExtends.ToString(), context); }
        if (src.HasMaxExtCommandUses) { dst.HasMaxExtCommandUses = true; dst.MaxExtCommandUses = src.MaxExtCommandUses; CarryOver(changes, "MaxExtCommandUses", src.MaxExtCommandUses.ToString(), context); }
        if (src.HasExtendTimePerExtends) { dst.HasExtendTimePerExtends = true; dst.ExtendTimePerExtends = src.ExtendTimePerExtends; CarryOver(changes, "ExtendTimePerExtends", src.ExtendTimePerExtends.ToString(), context); }
        if (src.HasMapTime) { dst.HasMapTime = true; dst.MapTime = src.MapTime; CarryOver(changes, "MapTime", src.MapTime.ToString(), context); }
        if (src.HasExtendRoundsPerExtends) { dst.HasExtendRoundsPerExtends = true; dst.ExtendRoundsPerExtends = src.ExtendRoundsPerExtends; CarryOver(changes, "ExtendRoundsPerExtends", src.ExtendRoundsPerExtends.ToString(), context); }
        if (src.HasMapRounds) { dst.HasMapRounds = true; dst.MapRounds = src.MapRounds; CarryOver(changes, "MapRounds", src.MapRounds.ToString(), context); }

        if (src.HasOnlyNomination) { dst.HasOnlyNomination = true; dst.OnlyNomination = src.OnlyNomination; CarryOver(changes, "OnlyNomination", src.OnlyNomination.ToString(), context); }
        if (src.HasMaxPlayers) { dst.HasMaxPlayers = true; dst.MaxPlayers = src.MaxPlayers; CarryOver(changes, "MaxPlayers", src.MaxPlayers.ToString(), context); }
        if (src.HasMinPlayers) { dst.HasMinPlayers = true; dst.MinPlayers = src.MinPlayers; CarryOver(changes, "MinPlayers", src.MinPlayers.ToString(), context); }
        if (src.HasProhibitAdminNomination) { dst.HasProhibitAdminNomination = true; dst.ProhibitAdminNomination = src.ProhibitAdminNomination; CarryOver(changes, "ProhibitAdminNomination", src.ProhibitAdminNomination.ToString(), context); }

        if (src.HasDaysAllowed)
        {
            dst.HasDaysAllowed = true;
            foreach (var d in src.DaysAllowed) dst.DaysAllowed.Add(d);
            CarryOver(changes, "DaysAllowed", string.Join(", ", src.DaysAllowed), context);
        }
        if (src.HasAllowedTimeRanges)
        {
            dst.HasAllowedTimeRanges = true;
            foreach (var r in src.AllowedTimeRanges) dst.AllowedTimeRanges.Add(r);
            CarryOver(changes, "AllowedTimeRanges", string.Join(", ", src.AllowedTimeRanges), context);
        }

        if (src.HasCooldown) { dst.HasCooldown = true; dst.Cooldown = src.Cooldown; CarryOver(changes, "Cooldown", src.Cooldown.ToString(), context); }

        // NominationSpecificCooldown → NominationCooldown (direct equivalent in new schema)
        if (src.HasNominationSpecificCooldown)
        {
            dst.HasNominationCooldown = true;
            dst.NominationCooldown = src.NominationSpecificCooldown;
            changes.Add(new MigrationChange(
                $"{context}.NominationSpecificCooldown → NominationCooldown",
                MigrationChangeKind.CarriedOver,
                src.NominationSpecificCooldown.ToString(),
                src.NominationSpecificCooldown.ToString()));
        }

        // Removed properties — record what's being dropped
        if (src.HasRestrictToAllowedUsersOnly)
            changes.Add(new MigrationChange($"{context}.RestrictToAllowedUsersOnly", MigrationChangeKind.Removed, src.RestrictToAllowedUsersOnly.ToString()));
        if (src.HasRequiredPermissions && src.RequiredPermissions.Count > 0)
            changes.Add(new MigrationChange($"{context}.RequiredPermissions", MigrationChangeKind.Removed, string.Join(", ", src.RequiredPermissions)));
        if (src.HasAllowedSteamIds && src.AllowedSteamIds.Count > 0)
            changes.Add(new MigrationChange($"{context}.AllowedSteamIds", MigrationChangeKind.Removed, string.Join(", ", src.AllowedSteamIds)));
        if (src.HasDisallowedSteamIds && src.DisallowedSteamIds.Count > 0)
            changes.Add(new MigrationChange($"{context}.DisallowedSteamIds", MigrationChangeKind.Removed, string.Join(", ", src.DisallowedSteamIds)));
        if (src.HasNominationCost)
            changes.Add(new MigrationChange($"{context}.NominationCost", MigrationChangeKind.Removed, src.NominationCost.ToString()));

        // Extras: Legacy uses flat string KV, Current uses typed KV — import as String kind
        foreach (var legacySection in src.Extras)
        {
            var section = new ExtraSection { Name = legacySection.Name };
            foreach (var entry in legacySection.Entries)
                section.Entries.Add(new ExtraKeyValue { Key = entry.Key, Value = entry.Value, Kind = ExtraValueKind.String });
            dst.Extras.Add(section);
        }
    }

    private static void CarryOver(List<MigrationChange> changes, string property, string value, string context)
    {
        changes.Add(new MigrationChange($"{context}.{property}", MigrationChangeKind.CarriedOver, value, value));
    }

    public static (IReadOnlyList<string> Removed, IReadOnlyList<string> Added) GetSchemaDiff()
    {
        return (RemovedProperties, AddedProperties);
    }
}

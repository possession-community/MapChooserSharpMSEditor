// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.Services.Legacy;

public static class LegacyConfigWriter
{
    public static void SaveFile(LegacyMapConfigFile file)
    {
        if (string.IsNullOrEmpty(file.FilePath))
            throw new InvalidOperationException("FilePath is not set.");

        var content = Serialize(file);
        File.WriteAllText(file.FilePath, content, new UTF8Encoding(false));
        file.IsDirty = false;
        Log.Debug("LegacyWriter",
            $"Wrote {file.FilePath} ({content.Length} bytes, groups={file.Groups.Count}, maps={file.Maps.Count})");
    }

    public static string Serialize(LegacyMapConfigFile file)
    {
        var sb = new StringBuilder();

        if (file.DefaultSettings is not null)
        {
            sb.AppendLine("[MapChooserSharpSettings.Default]");
            WriteProperties(sb, file.DefaultSettings, LegacyPropertyScope.Default);
            WriteExtras(sb, "MapChooserSharpSettings.Default", file.DefaultSettings);
            sb.AppendLine();
        }

        foreach (var g in file.Groups)
        {
            var header = $"MapChooserSharpSettings.Groups.{g.GroupName}";
            sb.Append('[').Append(header).AppendLine("]");
            WriteProperties(sb, g.Properties, LegacyPropertyScope.Group);
            WriteExtras(sb, header, g.Properties);
            sb.AppendLine();
        }

        foreach (var m in file.Maps)
        {
            sb.Append('[').Append(m.MapName).AppendLine("]");
            WriteProperties(sb, m.Properties, LegacyPropertyScope.Map);
            WriteExtras(sb, m.MapName, m.Properties);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void WriteProperties(StringBuilder sb, LegacyPropertySet p, LegacyPropertyScope scope)
    {
        var allowMapOnly = scope is LegacyPropertyScope.Default or LegacyPropertyScope.Map;

        if (allowMapOnly && p.HasMapNameAlias) sb.Append("MapNameAlias = ").AppendLine(Quote(p.MapNameAlias));
        if (allowMapOnly && p.HasMapDescription) sb.Append("MapDescription = ").AppendLine(Quote(p.MapDescription));
        if (p.HasIsDisabled) sb.Append("IsDisabled = ").AppendLine(p.IsDisabled ? "true" : "false");
        if (allowMapOnly && p.HasWorkshopId) sb.Append("WorkshopId = ").AppendLine(p.WorkshopId.ToString(CultureInfo.InvariantCulture));
        if (allowMapOnly && p.HasGroupSettings) sb.Append("GroupSettings = ").AppendLine(FormatStringArray(p.GroupSettings));
        if (p.HasOnlyNomination) sb.Append("OnlyNomination = ").AppendLine(p.OnlyNomination ? "true" : "false");
        if (p.HasRestrictToAllowedUsersOnly) sb.Append("RestrictToAllowedUsersOnly = ").AppendLine(p.RestrictToAllowedUsersOnly ? "true" : "false");
        if (p.HasProhibitAdminNomination) sb.Append("ProhibitAdminNomination = ").AppendLine(p.ProhibitAdminNomination ? "true" : "false");
        if (p.HasMaxExtends) sb.Append("MaxExtends = ").AppendLine(p.MaxExtends.ToString(CultureInfo.InvariantCulture));
        if (p.HasMaxExtCommandUses) sb.Append("MaxExtCommandUses = ").AppendLine(p.MaxExtCommandUses.ToString(CultureInfo.InvariantCulture));
        if (p.HasMapTime) sb.Append("MapTime = ").AppendLine(p.MapTime.ToString(CultureInfo.InvariantCulture));
        if (p.HasExtendTimePerExtends) sb.Append("ExtendTimePerExtends = ").AppendLine(p.ExtendTimePerExtends.ToString(CultureInfo.InvariantCulture));
        if (p.HasMapRounds) sb.Append("MapRounds = ").AppendLine(p.MapRounds.ToString(CultureInfo.InvariantCulture));
        if (p.HasExtendRoundsPerExtends) sb.Append("ExtendRoundsPerExtends = ").AppendLine(p.ExtendRoundsPerExtends.ToString(CultureInfo.InvariantCulture));
        if (p.HasMaxPlayers) sb.Append("MaxPlayers = ").AppendLine(p.MaxPlayers.ToString(CultureInfo.InvariantCulture));
        if (p.HasMinPlayers) sb.Append("MinPlayers = ").AppendLine(p.MinPlayers.ToString(CultureInfo.InvariantCulture));
        if (p.HasCooldown) sb.Append("Cooldown = ").AppendLine(p.Cooldown.ToString(CultureInfo.InvariantCulture));
        if (p.HasRequiredPermissions) sb.Append("RequiredPermissions = ").AppendLine(FormatStringArray(p.RequiredPermissions));
        if (p.HasAllowedSteamIds) sb.Append("AllowedSteamIds = ").AppendLine(FormatUlongArray(p.AllowedSteamIds));
        if (p.HasDisallowedSteamIds) sb.Append("DisallowedSteamIds = ").AppendLine(FormatUlongArray(p.DisallowedSteamIds));
        if (p.HasDaysAllowed) sb.Append("DaysAllowed = ").AppendLine(FormatDayArray(p.DaysAllowed));
        if (p.HasAllowedTimeRanges) sb.Append("AllowedTimeRanges = ").AppendLine(FormatTimeRangeArray(p.AllowedTimeRanges));
        if (p.HasNominationCost) sb.Append("NominationCost = ").AppendLine(p.NominationCost.ToString(CultureInfo.InvariantCulture));
        if (p.HasNominationSpecificCooldown) sb.Append("NominationSpecificCooldown = ").AppendLine(p.NominationSpecificCooldown.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteExtras(StringBuilder sb, string parentHeader, LegacyPropertySet p)
    {
        foreach (var section in p.Extras)
        {
            if (section.Entries.Count == 0)
                continue;
            sb.AppendLine();
            sb.Append('[').Append(parentHeader).Append(".extra.").Append(section.Name).AppendLine("]");
            foreach (var e in section.Entries)
                sb.Append(e.Key).Append(" = ").AppendLine(Quote(e.Value));
        }
    }

    private static string Quote(string s)
    {
        var escaped = s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
        return "\"" + escaped + "\"";
    }

    private static string FormatStringArray(IEnumerable<string> items) =>
        "[" + string.Join(", ", items.Select(Quote)) + "]";

    private static string FormatUlongArray(IEnumerable<ulong> items) =>
        "[" + string.Join(", ", items.Select(u => u.ToString(CultureInfo.InvariantCulture))) + "]";

    private static string FormatDayArray(IEnumerable<DayOfWeek> days) =>
        "[" + string.Join(", ", days.Select(d => Quote(d.ToString()))) + "]";

    private static string FormatTimeRangeArray(IEnumerable<TimeRangeSpec> ranges) =>
        "[" + string.Join(", ", ranges.Select(r => Quote(r.ToString()))) + "]";
}

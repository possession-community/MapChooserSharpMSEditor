using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Writes a <see cref="MapConfigFile"/> back to disk as TOML. Only properties with
/// HasX = true are emitted, so "unset" fields stay out of the file (matching the
/// server's null-means-inherit semantics).
/// </summary>
public static class TomlConfigWriter
{
    public static void SaveFile(MapConfigFile file)
    {
        if (string.IsNullOrEmpty(file.FilePath))
            throw new InvalidOperationException("FilePath is not set.");

        var content = Serialize(file);
        File.WriteAllText(file.FilePath, content, new UTF8Encoding(false));
        file.IsDirty = false;
        Log.Debug("Writer",
            $"Wrote {file.FilePath} ({content.Length} bytes, groups={file.Groups.Count}, maps={file.Maps.Count})");
    }

    public static string Serialize(MapConfigFile file)
    {
        var sb = new StringBuilder();

        if (file.DefaultSettings is not null)
        {
            sb.AppendLine("[MapChooserSharpSettings.Default]");
            WriteProperties(sb, file.DefaultSettings, PropertyScope.Default);
            WriteExtras(sb, "MapChooserSharpSettings.Default", file.DefaultSettings);
            sb.AppendLine();
        }

        foreach (var g in file.Groups)
        {
            var header = $"MapChooserSharpSettings.Groups.{g.GroupName}";
            sb.Append('[').Append(header).AppendLine("]");
            WriteProperties(sb, g.Properties, PropertyScope.Group);
            WriteExtras(sb, header, g.Properties);
            sb.AppendLine();

            foreach (var ov in g.DaySettings)
            {
                var ovHeader = $"{header}.DaySettings.{ov.Name}";
                sb.Append('[').Append(ovHeader).AppendLine("]");
                WriteOverrideProperties(sb, ov, PropertyScope.Group);
                WriteExtras(sb, ovHeader, ov.Properties);
                sb.AppendLine();
            }
        }

        foreach (var m in file.Maps)
        {
            sb.Append('[').Append(m.MapName).AppendLine("]");
            WriteProperties(sb, m.Properties, PropertyScope.Map);
            WriteExtras(sb, m.MapName, m.Properties);
            sb.AppendLine();

            foreach (var ov in m.DaySettings)
            {
                var ovHeader = $"{m.MapName}.DaySettings.{ov.Name}";
                sb.Append('[').Append(ovHeader).AppendLine("]");
                WriteOverrideProperties(sb, ov, PropertyScope.Map);
                WriteExtras(sb, ovHeader, ov.Properties);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void WriteProperties(StringBuilder sb, PropertySet p, PropertyScope scope)
    {
        // Map-only keys: groups reject MapNameAlias/MapDescription/WorkshopId/GroupSettings.
        var allowMapOnly = scope is PropertyScope.Default or PropertyScope.Map;
        // Group-only key: maps reject CooldownOverride.
        var allowGroupOnly = scope is PropertyScope.Default or PropertyScope.Group;

        if (allowMapOnly && p.HasMapNameAlias) sb.Append("MapNameAlias = ").AppendLine(Quote(p.MapNameAlias));
        if (allowMapOnly && p.HasMapDescription) sb.Append("MapDescription = ").AppendLine(Quote(p.MapDescription));
        if (allowMapOnly && p.HasWorkshopId) sb.Append("WorkshopId = ").AppendLine(p.WorkshopId.ToString(CultureInfo.InvariantCulture));
        if (p.HasIsDisabled) sb.Append("IsDisabled = ").AppendLine(p.IsDisabled ? "true" : "false");
        if (allowMapOnly && p.HasGroupSettings) sb.Append("GroupSettings = ").AppendLine(FormatStringArray(p.GroupSettings));
        if (allowGroupOnly && p.HasCooldownOverride) sb.Append("CooldownOverride = ").AppendLine(p.CooldownOverride.ToString(CultureInfo.InvariantCulture));
        if (p.HasMaxExtends) sb.Append("MaxExtends = ").AppendLine(p.MaxExtends.ToString(CultureInfo.InvariantCulture));
        if (p.HasMaxExtCommandUses) sb.Append("MaxExtCommandUses = ").AppendLine(p.MaxExtCommandUses.ToString(CultureInfo.InvariantCulture));
        if (p.HasExtendTimePerExtends) sb.Append("ExtendTimePerExtends = ").AppendLine(p.ExtendTimePerExtends.ToString(CultureInfo.InvariantCulture));
        if (p.HasMapTime) sb.Append("MapTime = ").AppendLine(p.MapTime.ToString(CultureInfo.InvariantCulture));
        if (p.HasExtendRoundsPerExtends) sb.Append("ExtendRoundsPerExtends = ").AppendLine(p.ExtendRoundsPerExtends.ToString(CultureInfo.InvariantCulture));
        if (p.HasMapRounds) sb.Append("MapRounds = ").AppendLine(p.MapRounds.ToString(CultureInfo.InvariantCulture));
        if (p.HasOnlyNomination) sb.Append("OnlyNomination = ").AppendLine(p.OnlyNomination ? "true" : "false");
        if (p.HasMaxPlayers) sb.Append("MaxPlayers = ").AppendLine(p.MaxPlayers.ToString(CultureInfo.InvariantCulture));
        if (p.HasMinPlayers) sb.Append("MinPlayers = ").AppendLine(p.MinPlayers.ToString(CultureInfo.InvariantCulture));
        if (p.HasProhibitAdminNomination) sb.Append("ProhibitAdminNomination = ").AppendLine(p.ProhibitAdminNomination ? "true" : "false");
        if (p.HasDaysAllowed) sb.Append("DaysAllowed = ").AppendLine(FormatDayArray(p.DaysAllowed));
        if (p.HasAllowedTimeRanges) sb.Append("AllowedTimeRanges = ").AppendLine(FormatTimeRangeArray(p.AllowedTimeRanges));
        if (p.HasCooldown) sb.Append("Cooldown = ").AppendLine(p.Cooldown.ToString(CultureInfo.InvariantCulture));
        if (p.HasCooldownDateTime) sb.Append("CooldownDateTime = ").AppendLine(Quote(p.CooldownDateTime));
    }

    private static void WriteOverrideProperties(StringBuilder sb, DaySettingsOverrideModel ov, PropertyScope scope)
    {
        sb.Append("Enabled = ").AppendLine(ov.Enabled ? "true" : "false");
        sb.Append("ForceOverride = ").AppendLine(ov.ForceOverride ? "true" : "false");
        sb.Append("OverridePriority = ").AppendLine(ov.OverridePriority.ToString(CultureInfo.InvariantCulture));
        sb.Append("TargetDays = ").AppendLine(FormatDayArray(ov.TargetDays));
        if (ov.TargetTimeRanges.Count > 0)
            sb.Append("TargetTimeRanges = ").AppendLine(FormatTimeRangeArray(ov.TargetTimeRanges));
        WriteProperties(sb, ov.Properties, scope);
    }

    private static void WriteExtras(StringBuilder sb, string parentHeader, PropertySet p)
    {
        foreach (var section in p.Extras)
        {
            if (section.Entries.Count == 0)
                continue;
            sb.AppendLine();
            sb.Append('[').Append(parentHeader).Append(".extra.").Append(section.Name).AppendLine("]");
            foreach (var e in section.Entries)
                sb.Append(e.Key).Append(" = ").AppendLine(FormatExtraValue(e));
        }
    }

    private static string FormatExtraValue(ExtraKeyValue e) => e.Kind switch
    {
        ExtraValueKind.String => Quote(e.Value),
        ExtraValueKind.Integer => long.TryParse(e.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i.ToString(CultureInfo.InvariantCulture) : "0",
        ExtraValueKind.Float => double.TryParse(e.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d.ToString("R", CultureInfo.InvariantCulture) : "0.0",
        ExtraValueKind.Boolean => string.Equals(e.Value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false",
        _ => Quote(e.Value),
    };

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

    private static string FormatDayArray(IEnumerable<DayOfWeek> days) =>
        "[" + string.Join(", ", days.Select(d => Quote(d.ToString().ToLowerInvariant()))) + "]";

    private static string FormatTimeRangeArray(IEnumerable<TimeRangeSpec> ranges) =>
        "[" + string.Join(", ", ranges.Select(r => Quote(r.ToString()))) + "]";
}

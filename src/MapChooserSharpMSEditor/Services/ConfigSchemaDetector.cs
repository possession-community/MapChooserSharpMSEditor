using System.IO;

namespace MapChooserSharpMSEditor.Services;

public enum ConfigSchemaKind
{
    /// <summary>No distinguishing markers found — the file only uses keys common to both
    /// schemas, so we can't tell. Callers should fall back to the active mode.</summary>
    Ambiguous,
    /// <summary>Current (MapChooserSharpMS) schema — has DaySettings / CooldownOverride /
    /// CooldownDateTime keys that Legacy never emits.</summary>
    Current,
    /// <summary>Legacy (MapChooserSharp.API v0.1.5) schema — has nomination keys
    /// (RestrictToAllowedUsersOnly / AllowedSteamIds / NominationCost ...) that Current
    /// never emits.</summary>
    Legacy,
}

/// <summary>
/// Sniffs TOML content for schema-exclusive property names so the editor can warn when
/// a file opened in mode X looks like it belongs to mode Y. Substring search is good
/// enough — the marker keys are unique enough that false positives are effectively
/// impossible (they'd require the string to appear inside a quoted value).
/// </summary>
public static class ConfigSchemaDetector
{
    // Only-Current markers. DaySettings appears as a sub-table header
    // ([mapname.DaySettings.x]); the others appear as property assignments.
    private static readonly string[] CurrentMarkers =
    {
        ".DaySettings.",
        "CooldownDateTime",
        "CooldownOverride",
        "NominationCooldown",
        "NominationCooldownDateTime",
    };

    // Only-Legacy markers. All property assignments on Default / Group / Map sections.
    private static readonly string[] LegacyMarkers =
    {
        "RestrictToAllowedUsersOnly",
        "RequiredPermissions",
        "AllowedSteamIds",
        "DisallowedSteamIds",
        "NominationCost",
        "NominationSpecificCooldown",
    };

    public static ConfigSchemaKind DetectFromText(string? content)
    {
        if (string.IsNullOrEmpty(content)) return ConfigSchemaKind.Ambiguous;

        var hasCurrent = false;
        foreach (var m in CurrentMarkers)
            if (content.Contains(m)) { hasCurrent = true; break; }

        var hasLegacy = false;
        foreach (var m in LegacyMarkers)
            if (content.Contains(m)) { hasLegacy = true; break; }

        // Both markers present is odd — means the file was hand-edited across schemas.
        // Treat as Current since Current is a superset minus the nomination extras.
        if (hasCurrent) return ConfigSchemaKind.Current;
        if (hasLegacy) return ConfigSchemaKind.Legacy;
        return ConfigSchemaKind.Ambiguous;
    }

    public static ConfigSchemaKind DetectFromFile(string path)
    {
        try
        {
            return DetectFromText(File.ReadAllText(path));
        }
        catch
        {
            return ConfigSchemaKind.Ambiguous;
        }
    }

    /// <summary>
    /// Detect the dominant schema for a folder by sampling <paramref name="maxFiles"/>
    /// .toml files (recursive). Used by Open Folder before the editor commits to loading
    /// a potentially mismatched workspace.
    /// </summary>
    public static ConfigSchemaKind DetectFromFolder(string folderPath, int maxFiles = 5)
    {
        if (!Directory.Exists(folderPath)) return ConfigSchemaKind.Ambiguous;

        var currentHits = 0;
        var legacyHits = 0;
        var sampled = 0;

        try
        {
            // Root-level default.toml is the richest single signal — it always carries
            // the marker keys we care about — so bias toward sampling it first.
            var preferred = Path.Combine(folderPath, "default.toml");
            if (File.Exists(preferred))
            {
                Tally(DetectFromFile(preferred));
                sampled++;
            }

            foreach (var f in Directory.EnumerateFiles(folderPath, "*.toml", SearchOption.AllDirectories))
            {
                if (sampled >= maxFiles) break;
                if (string.Equals(f, preferred, System.StringComparison.OrdinalIgnoreCase)) continue;
                Tally(DetectFromFile(f));
                sampled++;
            }
        }
        catch
        {
            return ConfigSchemaKind.Ambiguous;
        }

        void Tally(ConfigSchemaKind k)
        {
            if (k == ConfigSchemaKind.Current) currentHits++;
            else if (k == ConfigSchemaKind.Legacy) legacyHits++;
        }

        if (currentHits > legacyHits) return ConfigSchemaKind.Current;
        if (legacyHits > currentHits) return ConfigSchemaKind.Legacy;
        return ConfigSchemaKind.Ambiguous;
    }
}

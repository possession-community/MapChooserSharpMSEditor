using System;
using System.IO;
using System.Text.Json;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Persistent per-user preferences. Currently only the UI locale. Stored as JSON at
/// <c>%AppData%\MapChooserSharpMSEditor\settings.json</c> (or the platform equivalent
/// for non-Windows). Corrupted files are silently ignored so a bad file never blocks startup.
/// </summary>
public static class UserSettings
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MapChooserSharpMSEditor",
        "settings.json");

    /// <summary>Explicit locale override. null = auto-detect from OS.</summary>
    public static string? Locale { get; private set; }

    static UserSettings() => Load();

    public static void Load()
    {
        try
        {
            if (!File.Exists(Path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(Path));
            if (doc.RootElement.TryGetProperty("locale", out var loc) && loc.ValueKind == JsonValueKind.String)
                Locale = loc.GetString();
        }
        catch { /* ignore — missing/corrupt settings just means we use defaults */ }
    }

    public static void SetLocale(string? locale)
    {
        Locale = string.IsNullOrWhiteSpace(locale) ? null : locale;
        Save();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var json = JsonSerializer.Serialize(new { locale = Locale }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path, json);
        }
        catch { /* best-effort — failing to save isn't worth blocking the UI */ }
    }
}

using System;
using System.IO;
using MapChooserSharpMSEditor.Services;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: SmokeTest <config.toml>");
    return 2;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"file not found: {path}");
    return 2;
}

Console.WriteLine($"=== Loading {path} ===");
var file = TomlConfigLoader.LoadFile(path);
Console.WriteLine($"Default: {(file.DefaultSettings is null ? "none" : "present")}");
Console.WriteLine($"Groups:  {file.Groups.Count}");
foreach (var g in file.Groups)
    Console.WriteLine($"  - {g.GroupName} (DaySettings: {g.DaySettings.Count})");
Console.WriteLine($"Maps:    {file.Maps.Count}");
foreach (var m in file.Maps)
    Console.WriteLine($"  - {m.MapName} (DaySettings: {m.DaySettings.Count})");

Console.WriteLine();
Console.WriteLine("=== Re-serialized TOML ===");
Console.WriteLine(TomlConfigWriter.Serialize(file));
return 0;

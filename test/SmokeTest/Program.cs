using System;
using System.IO;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.Services.Legacy;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: SmokeTest [--legacy] <config.toml>");
    return 2;
}

var legacy = false;
var pathArg = args[0];
if (pathArg == "--legacy")
{
    if (args.Length < 2) { Console.Error.WriteLine("usage: SmokeTest --legacy <config.toml>"); return 2; }
    legacy = true;
    pathArg = args[1];
}

if (!File.Exists(pathArg))
{
    Console.Error.WriteLine($"file not found: {pathArg}");
    return 2;
}

if (legacy)
{
    Console.WriteLine($"=== Loading (LEGACY) {pathArg} ===");
    var lfile = LegacyConfigLoader.LoadFile(pathArg);
    Console.WriteLine($"Default: {(lfile.DefaultSettings is null ? "none" : "present")}");
    Console.WriteLine($"Groups:  {lfile.Groups.Count}");
    foreach (var g in lfile.Groups) Console.WriteLine($"  - {g.GroupName}");
    Console.WriteLine($"Maps:    {lfile.Maps.Count}");
    foreach (var m in lfile.Maps) Console.WriteLine($"  - {m.MapName}");

    Console.WriteLine();
    Console.WriteLine("=== Re-serialized TOML ===");
    Console.WriteLine(LegacyConfigWriter.Serialize(lfile));
    return 0;
}

Console.WriteLine($"=== Loading {pathArg} ===");
var file = TomlConfigLoader.LoadFile(pathArg);
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

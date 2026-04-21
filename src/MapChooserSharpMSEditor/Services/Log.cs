using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace MapChooserSharpMSEditor.Services;

public enum LogLevel { Debug, Info, Warn, Error }

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Source, string Message)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");
    public string LevelText => Level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info  => "INF",
        LogLevel.Warn  => "WRN",
        LogLevel.Error => "ERR",
        _ => "???",
    };
}

/// <summary>
/// Session-wide debug log. One shared <see cref="Entries"/> collection drives the
/// console panel; additions always marshal to the UI thread so off-thread call sites
/// (HTTP callbacks, async work) don't corrupt the ObservableCollection.
/// <para>Capped at <see cref="MaxEntries"/> — oldest entries drop first when full so
/// long-running sessions don't balloon memory.</para>
/// </summary>
public static class Log
{
    public const int MaxEntries = 2000;

    public static ObservableCollection<LogEntry> Entries { get; } = new();

    public static void Debug(string source, string message) => Add(LogLevel.Debug, source, message);
    public static void Info (string source, string message) => Add(LogLevel.Info,  source, message);
    public static void Warn (string source, string message) => Add(LogLevel.Warn,  source, message);
    public static void Error(string source, string message) => Add(LogLevel.Error, source, message);

    public static void Clear()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Clear);
            return;
        }
        Entries.Clear();
    }

    private static void Add(LogLevel level, string source, string message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Add(level, source, message));
            return;
        }
        Entries.Add(new LogEntry(DateTime.Now, level, source, message));
        while (Entries.Count > MaxEntries) Entries.RemoveAt(0);
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Shell-out wrapper around the <c>git</c> CLI. Intentionally shell-based rather than
/// via a library so no extra package is added and behavior mirrors exactly what the user
/// would see on the command line.
/// </summary>
public static class GitService
{
    /// <summary>
    /// Walk upward from <paramref name="startPath"/> looking for a <c>.git</c> directory or
    /// file (git worktree uses a file). Returns the directory that contains it, or null if
    /// none is found up to the filesystem root.
    /// </summary>
    public static string? FindRepoRoot(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath)) return null;
        var dir = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : new DirectoryInfo(Path.GetDirectoryName(startPath) ?? startPath);

        for (var d = dir; d is not null; d = d.Parent)
        {
            var gitPath = Path.Combine(d.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
                return d.FullName;
        }
        return null;
    }

    /// <summary>
    /// Return every local + remote branch name. Output order mirrors
    /// <c>git for-each-ref</c> which roughly sorts by ref type then name.
    /// </summary>
    public static IReadOnlyList<string> ListBranches(string repoRoot)
    {
        var result = new List<string>();
        if (!Run(repoRoot, "for-each-ref --format=%(refname:short) refs/heads refs/remotes", out var stdout))
            return result;
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Trim();
            // Skip the symbolic remote-HEAD entry (e.g. "origin/HEAD") — it points to
            // another real branch and would just create a confusing duplicate.
            if (string.IsNullOrEmpty(name) || name.EndsWith("/HEAD")) continue;
            result.Add(name);
        }
        return result;
    }

    public static string? GetCurrentBranch(string repoRoot)
    {
        if (!Run(repoRoot, "rev-parse --abbrev-ref HEAD", out var stdout)) return null;
        var name = stdout.Trim();
        return string.IsNullOrEmpty(name) || name == "HEAD" ? null : name;
    }

    /// <summary>
    /// Read <paramref name="relativePath"/> at the given branch. Returns null if the file
    /// doesn't exist in that branch (git exits non-zero). <paramref name="relativePath"/>
    /// is normalized to forward slashes since git uses posix separators internally.
    /// </summary>
    public static string? ReadFileAtBranch(string repoRoot, string branch, string relativePath)
    {
        var gitPath = relativePath.Replace('\\', '/');
        // Quote the branch:path spec to survive unusual characters; single quotes don't
        // nest on Windows, so rely on Process's arg-escaping via ArgumentList instead.
        if (!RunArgs(repoRoot, out var stdout, "show", $"{branch}:{gitPath}")) return null;
        return stdout;
    }

    /// <summary>
    /// Bulk-read many paths from a single branch via one long-running <c>git cat-file</c>
    /// session. Vastly faster than calling <see cref="ReadFileAtBranch"/> in a loop because
    /// process-spawn overhead (~50-100ms per call on Windows) is paid exactly once.
    ///
    /// <para>Returns a dict keyed by the <i>input</i> relative path (forward-slash
    /// normalized). Missing entries map to null — caller can distinguish "not in branch"
    /// from "empty file".</para>
    /// </summary>
    public static Dictionary<string, string?> BatchReadFiles(
        string repoRoot, string branch, IReadOnlyList<string> relativePaths)
    {
        var result = new Dictionary<string, string?>(relativePaths.Count, StringComparer.Ordinal);
        if (relativePaths.Count == 0) return result;

        // --buffer defers per-blob stdout flushes; git will still block when the pipe
        // buffer fills (it has to, the blob data has to go somewhere), so we also have
        // to drain stdout on a background thread while we feed stdin from the foreground.
        // Without the background reader, writing N queries + reading N responses
        // serially deadlocks as soon as total response size exceeds the OS pipe buffer
        // (~64KB on Windows) — which 100 small map files easily exceed.
        var psi = new ProcessStartInfo("git", "cat-file --batch --buffer")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return result;

            var stdout = p.StandardOutput.BaseStream;
            var stderrTask = p.StandardError.ReadToEndAsync();

            // Background reader: consume responses as fast as git can produce them.
            var readerThread = new System.Threading.Thread(() =>
            {
                try
                {
                    for (var i = 0; i < relativePaths.Count; i++)
                    {
                        var header = ReadLineBinary(stdout);
                        if (header is null) break;

                        // Missing: "<query> missing". Query echoed verbatim, so safest match
                        // is just the trailing " missing" token.
                        if (header.EndsWith(" missing", StringComparison.Ordinal))
                        {
                            result[relativePaths[i]] = null;
                            continue;
                        }

                        // Normal: "<sha> <type> <size>"
                        var parts = header.Split(' ');
                        if (parts.Length < 3 || !long.TryParse(parts[2], out var size))
                        {
                            Log.Warn("Git", $"cat-file: unexpected header '{header}' for {relativePaths[i]}");
                            result[relativePaths[i]] = null;
                            continue;
                        }

                        var buf = new byte[size];
                        var read = 0;
                        while (read < size)
                        {
                            var n = stdout.Read(buf, read, (int)size - read);
                            if (n <= 0) break;
                            read += n;
                        }
                        // Trailing \n after each blob — consume it so the next header
                        // reads cleanly.
                        if (read == size) stdout.ReadByte();

                        result[relativePaths[i]] = Encoding.UTF8.GetString(buf, 0, read);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Git", $"cat-file reader thread error: {ex.Message}");
                }
            });
            readerThread.IsBackground = true;
            readerThread.Start();

            var stdin = p.StandardInput;
            for (var i = 0; i < relativePaths.Count; i++)
            {
                var gitPath = relativePaths[i].Replace('\\', '/');
                stdin.WriteLine($"{branch}:{gitPath}");
            }
            stdin.Flush();
            stdin.Close();

            readerThread.Join(TimeSpan.FromSeconds(30));
            p.WaitForExit(5000);

            // Surface git's stderr if something looked off. Only logged — the per-path
            // "missing" result already conveys the user-facing signal.
            var err = stderrTask.Result;
            if (!string.IsNullOrWhiteSpace(err))
                Log.Debug("Git", $"cat-file stderr: {err.Trim()}");
        }
        catch (Exception ex)
        {
            Log.Warn("Git", $"cat-file --batch failed: {ex.Message}");
        }
        return result;
    }

    /// <summary>Read one LF-terminated line from a binary stream as ASCII.</summary>
    private static string? ReadLineBinary(Stream s)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var b = s.ReadByte();
            if (b < 0) return sb.Length == 0 ? null : sb.ToString();
            if (b == '\n') return sb.ToString();
            if (b == '\r') continue;
            sb.Append((char)b);
        }
    }

    private static bool Run(string workingDir, string args, out string stdout)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        return ExecuteAndRead(psi, out stdout);
    }

    private static bool RunArgs(string workingDir, out string stdout, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return ExecuteAndRead(psi, out stdout);
    }

    private static bool ExecuteAndRead(ProcessStartInfo psi, out string stdout)
    {
        stdout = "";
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return false;
            stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Warn("Git", $"git invocation failed: {ex.Message}");
            return false;
        }
    }
}

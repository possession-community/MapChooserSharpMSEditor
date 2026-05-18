// LEGACY — remove when MCS migration completes
using System.Collections.Generic;
using System.IO;
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.Services.Legacy;

public sealed record LegacyBranchSnapshot(
    LegacyProjectContext Project,
    IReadOnlyList<string> Warnings,
    int LoadedCount,
    int MissingCount);

/// <summary>
/// Loads every file of a <see cref="LegacyProjectContext"/> from another git branch so
/// the diff tool can compute effective values against that branch's state. Files that
/// don't exist in the target branch (e.g. newly added in the working copy) are skipped
/// and reported via <see cref="LegacyBranchSnapshot.Warnings"/>.
/// </summary>
public static class LegacyBranchSnapshotLoader
{
    public static LegacyBranchSnapshot Load(LegacyProjectContext source, string repoRoot, string branch)
    {
        var shadow = new LegacyProjectContext();
        var warnings = new List<string>();
        var loaded = 0;
        var missing = 0;

        // Resolve every loaded file to its repo-relative path first, then ask git for all
        // blob contents in a single cat-file invocation. Going one-by-one used to pay
        // ~50-100ms of process-spawn overhead per file on Windows, which dominated
        // snapshot time for 100+ map workspaces.
        var entries = new List<(LegacyMapConfigFile Src, string Rel)>();
        foreach (var src in source.Files)
        {
            if (string.IsNullOrEmpty(src.FilePath))
            {
                warnings.Add($"Skipped unsaved file '{src.DisplayName}'.");
                continue;
            }
            string rel;
            try { rel = Path.GetRelativePath(repoRoot, src.FilePath); }
            catch { warnings.Add($"Skipped '{src.DisplayName}': path not under repo root."); continue; }

            if (rel.StartsWith("..", System.StringComparison.Ordinal))
            {
                warnings.Add($"Skipped '{src.DisplayName}': outside repo '{repoRoot}'.");
                continue;
            }
            entries.Add((src, rel.Replace('\\', '/')));
        }

        var blobs = GitService.BatchReadFiles(repoRoot, branch, entries.ConvertAll(e => e.Rel));

        foreach (var (src, rel) in entries)
        {
            if (!blobs.TryGetValue(rel, out var content) || content is null)
            {
                missing++;
                warnings.Add($"'{rel}' not found on branch '{branch}'.");
                continue;
            }

            try
            {
                var file = LegacyConfigLoader.LoadFromText(content, src.DisplayName, src.FilePath);
                shadow.Add(file);
                loaded++;
            }
            catch (System.Exception ex)
            {
                warnings.Add($"Parse failed for '{rel}' on '{branch}': {ex.Message}");
            }
        }

        Log.Info("LegacyBranchDiff",
            $"Snapshot branch={branch} loaded={loaded} missing={missing} warn={warnings.Count}");
        return new LegacyBranchSnapshot(shadow, warnings, loaded, missing);
    }
}

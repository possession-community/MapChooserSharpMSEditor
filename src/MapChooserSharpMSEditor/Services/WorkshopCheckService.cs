using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MapChooserSharpMSEditor.Services;

public enum WorkshopStatus
{
    Public,
    /// <summary>EResult 9 — not found / deleted / private / unlisted.
    /// Steam collapses these into a single code so we can't distinguish them here.</summary>
    NotFoundOrPrivate,
    /// <summary>Network error, JSON parse failure, etc. — treat as "don't know" so the
    /// UI can show it separately from confirmed-private items.</summary>
    Error,
}

public sealed record WorkshopCheckResult(ulong PublishedFileId, WorkshopStatus Status, string? Title, string? ErrorMessage);

/// <summary>
/// Batch workshop-item status lookup. Uses the unauthenticated Steam Remote Storage
/// endpoint (<c>ISteamRemoteStorage/GetPublishedFileDetails/v1/</c>) so no API key is
/// required — Steam itself gates public-item metadata behind no auth, and it returns
/// EResult=9 for items that would otherwise 404 (deleted / private / unlisted).
///
/// Chunking at 100 IDs per request keeps request bodies well under the endpoint's
/// accepted size while still fitting most workspaces in a single round-trip.
/// </summary>
public sealed class WorkshopCheckService
{
    private const int BatchSize = 100;
    private const string Endpoint = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

    private static readonly HttpClient _http = new();

    public async Task<List<WorkshopCheckResult>> CheckAsync(
        IReadOnlyList<ulong> ids,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        Log.Info("Workshop", $"CheckAsync: {ids.Count} id(s), batch size {BatchSize}");
        var results = new List<WorkshopCheckResult>(ids.Count);
        var total = ids.Count;
        var done = 0;

        for (var offset = 0; offset < ids.Count; offset += BatchSize)
        {
            var batch = ids.Skip(offset).Take(BatchSize).ToList();
            try
            {
                var batchResults = await CheckOneBatchAsync(batch, ct);
                results.AddRange(batchResults);
                Log.Debug("Workshop",
                    $"Batch ok: offset={offset} size={batch.Count} ({batchResults.Count(r => r.Status == WorkshopStatus.Public)} public, {batchResults.Count(r => r.Status != WorkshopStatus.Public)} non-public)");
            }
            catch (Exception ex)
            {
                Log.Error("Workshop", $"Batch failed at offset {offset}: {ex.Message}");
                foreach (var id in batch)
                    results.Add(new WorkshopCheckResult(id, WorkshopStatus.Error, null, ex.Message));
            }

            done += batch.Count;
            progress?.Report((done, total));
        }

        Log.Info("Workshop", $"CheckAsync complete: {results.Count(r => r.Status == WorkshopStatus.NotFoundOrPrivate)} private/deleted, {results.Count(r => r.Status == WorkshopStatus.Error)} errored");
        return results;
    }

    private static async Task<List<WorkshopCheckResult>> CheckOneBatchAsync(List<ulong> ids, CancellationToken ct)
    {
        // Steam's form-urlencoded body for this endpoint: itemcount + one indexed field per id.
        var form = new List<KeyValuePair<string, string>>(ids.Count + 1)
        {
            new("itemcount", ids.Count.ToString()),
        };
        for (var i = 0; i < ids.Count; i++)
            form.Add(new($"publishedfileids[{i}]", ids[i].ToString()));

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(Endpoint, content, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        var parsed = System.Text.Json.JsonSerializer.Deserialize<SteamEnvelope>(json);
        var details = parsed?.Response?.PublishedFileDetails ?? new List<SteamDetail>();

        // Walk the input order so the caller's list aligns with its request. Missing rows
        // are unlikely (Steam returns a placeholder for every id) but default to Error
        // defensively if one goes missing.
        var byId = details.ToDictionary(d => d.PublishedFileIdValue, d => d);
        var results = new List<WorkshopCheckResult>(ids.Count);
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var d))
            {
                results.Add(new WorkshopCheckResult(id, WorkshopStatus.Error, null, "no response row"));
                continue;
            }
            var status = d.Result switch
            {
                1 => WorkshopStatus.Public,
                9 => WorkshopStatus.NotFoundOrPrivate,
                _ => WorkshopStatus.Error,
            };
            results.Add(new WorkshopCheckResult(id, status, d.Title, status == WorkshopStatus.Error ? $"result={d.Result}" : null));
        }
        return results;
    }

    // ===== JSON DTOs =====
    // Minimal shapes; Steam returns many more fields we don't care about.

    private sealed class SteamEnvelope
    {
        [JsonPropertyName("response")] public SteamResponse? Response { get; set; }
    }

    private sealed class SteamResponse
    {
        [JsonPropertyName("publishedfiledetails")] public List<SteamDetail>? PublishedFileDetails { get; set; }
    }

    private sealed class SteamDetail
    {
        [JsonPropertyName("publishedfileid")] public string? PublishedFileId { get; set; }
        [JsonPropertyName("result")] public int Result { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }

        public ulong PublishedFileIdValue =>
            ulong.TryParse(PublishedFileId, out var v) ? v : 0;
    }
}

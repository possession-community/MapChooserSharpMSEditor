using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.Services;

public sealed record CollectionItem(long WorkshopId, string Title, bool IsPublic);

public sealed class CollectionGenerateService : IDisposable
{
    private const int BatchSize = 100;
    private const string CollectionEndpoint = "https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/";
    private const string FallbackDetailsEndpoint = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
    private const string AuthDetailsEndpoint = "https://api.steampowered.com/IPublishedFileService/GetDetails/v1/";

    private readonly HttpClient _http = new();

    public void Dispose() => _http.Dispose();

    public async Task<List<long>> FetchCollectionItemIdsAsync(string collectionId, CancellationToken ct = default)
    {
        Log.Info("Collection", $"Fetching collection {collectionId}");
        var form = new List<KeyValuePair<string, string>>
        {
            new("collectioncount", "1"),
            new("publishedfileids[0]", collectionId),
        };

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(CollectionEndpoint, content, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        var parsed = JsonSerializer.Deserialize<CollectionEnvelope>(json);
        var details = parsed?.Response?.CollectionDetails;
        if (details is null || details.Count == 0)
            return [];

        var ids = details[0].Children?
            .Where(c => long.TryParse(c.PublishedFileId, out _))
            .Select(c => long.Parse(c.PublishedFileId!))
            .ToList() ?? [];

        Log.Info("Collection", $"Found {ids.Count} item(s) in collection");
        return ids;
    }

    public async Task<List<CollectionItem>> FetchItemDetailsAsync(
        IReadOnlyList<long> ids, string? apiKey = null,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var useAuth = !string.IsNullOrWhiteSpace(apiKey);
        Log.Info("Collection", $"Fetching details for {ids.Count} item(s), auth={useAuth}");

        var results = new List<CollectionItem>(ids.Count);
        var done = 0;

        for (var offset = 0; offset < ids.Count; offset += BatchSize)
        {
            var batch = ids.Skip(offset).Take(BatchSize).ToList();
            var batchResults = useAuth
                ? await FetchAuthBatchAsync(batch, apiKey!, ct)
                : await FetchFallbackBatchAsync(batch, ct);
            results.AddRange(batchResults);
            done += batch.Count;
            progress?.Report((done, ids.Count));
        }

        return results;
    }

    public MapConfigFile GenerateConfig(IReadOnlyList<CollectionItem> items, string displayName)
    {
        var file = new MapConfigFile { DisplayName = displayName };
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var mapName = SanitizeMapName(item.Title, usedNames);
            usedNames.Add(mapName);

            var map = new MapEntryModel { MapName = mapName };
            map.Properties.HasWorkshopId = true;
            map.Properties.WorkshopId = item.WorkshopId;
            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                map.Properties.HasMapNameAlias = true;
                map.Properties.MapNameAlias = item.Title;
            }
            if (!item.IsPublic)
            {
                map.Properties.HasIsDisabled = true;
                map.Properties.IsDisabled = true;
            }
            file.Maps.Add(map);
        }

        Log.Info("Collection", $"Generated config with {file.Maps.Count} map(s)");
        return file;
    }

    private static string SanitizeMapName(string title, HashSet<string> usedNames)
    {
        if (string.IsNullOrWhiteSpace(title))
            title = "unnamed_map";

        var sanitized = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9_]", "_");
        sanitized = Regex.Replace(sanitized, @"_+", "_").Trim('_');
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "unnamed_map";

        var candidate = sanitized;
        var i = 2;
        while (usedNames.Contains(candidate))
            candidate = $"{sanitized}_{i++}";

        return candidate;
    }

    private async Task<List<CollectionItem>> FetchFallbackBatchAsync(List<long> ids, CancellationToken ct)
    {
        var form = new List<KeyValuePair<string, string>>(ids.Count + 1)
        {
            new("itemcount", ids.Count.ToString()),
        };
        for (var i = 0; i < ids.Count; i++)
            form.Add(new($"publishedfileids[{i}]", ids[i].ToString()));

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(FallbackDetailsEndpoint, content, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        var parsed = JsonSerializer.Deserialize<FallbackEnvelope>(json);
        var details = parsed?.Response?.PublishedFileDetails ?? [];

        var byId = details
            .Where(d => long.TryParse(d.PublishedFileId, out _))
            .ToDictionary(d => long.Parse(d.PublishedFileId!), d => d);

        return ids.Select(id =>
        {
            if (!byId.TryGetValue(id, out var d))
                return new CollectionItem(id, $"workshop_{id}", false);
            return new CollectionItem(id, d.Title ?? $"workshop_{id}", d.Result == 1);
        }).ToList();
    }

    private async Task<List<CollectionItem>> FetchAuthBatchAsync(List<long> ids, string apiKey, CancellationToken ct)
    {
        var queryParts = new List<string> { $"key={apiKey}" };
        for (var i = 0; i < ids.Count; i++)
            queryParts.Add($"publishedfileids[{i}]={ids[i]}");

        var url = $"{AuthDetailsEndpoint}?{string.Join("&", queryParts)}";
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        var parsed = JsonSerializer.Deserialize<AuthEnvelope>(json);
        var details = parsed?.Response?.PublishedFileDetails ?? [];

        var byId = new Dictionary<long, AuthDetail>();
        foreach (var d in details)
            if (long.TryParse(d.PublishedFileId, out var fid))
                byId[fid] = d;

        return ids.Select(id =>
        {
            if (!byId.TryGetValue(id, out var d))
                return new CollectionItem(id, $"workshop_{id}", false);
            var isPublic = d.Result == 1 && d.Visibility == 0;
            return new CollectionItem(id, d.Title ?? $"workshop_{id}", isPublic);
        }).ToList();
    }

    // JSON DTOs — Collection
    private sealed class CollectionEnvelope
    {
        [JsonPropertyName("response")] public CollectionResponse? Response { get; set; }
    }
    private sealed class CollectionResponse
    {
        [JsonPropertyName("collectiondetails")] public List<CollectionDetail>? CollectionDetails { get; set; }
    }
    private sealed class CollectionDetail
    {
        [JsonPropertyName("children")] public List<CollectionChild>? Children { get; set; }
    }
    private sealed class CollectionChild
    {
        [JsonPropertyName("publishedfileid")] public string? PublishedFileId { get; set; }
    }

    // JSON DTOs — Fallback (ISteamRemoteStorage)
    private sealed class FallbackEnvelope
    {
        [JsonPropertyName("response")] public FallbackResponse? Response { get; set; }
    }
    private sealed class FallbackResponse
    {
        [JsonPropertyName("publishedfiledetails")] public List<FallbackDetail>? PublishedFileDetails { get; set; }
    }
    private sealed class FallbackDetail
    {
        [JsonPropertyName("publishedfileid")] public string? PublishedFileId { get; set; }
        [JsonPropertyName("result")] public int Result { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
    }

    // JSON DTOs — Auth (IPublishedFileService)
    private sealed class AuthEnvelope
    {
        [JsonPropertyName("response")] public AuthResponse? Response { get; set; }
    }
    private sealed class AuthResponse
    {
        [JsonPropertyName("publishedfiledetails")] public List<AuthDetail>? PublishedFileDetails { get; set; }
    }
    private sealed class AuthDetail
    {
        [JsonPropertyName("publishedfileid")] public string? PublishedFileId { get; set; }
        [JsonPropertyName("result")] public int Result { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("visibility")] public int Visibility { get; set; }
    }
}

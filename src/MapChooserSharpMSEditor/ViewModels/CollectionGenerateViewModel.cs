using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels;

public partial class CollectionGenerateViewModel : ViewModelBase
{
    [ObservableProperty] private string _collectionId = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private int _publicCount;
    [ObservableProperty] private int _nonPublicCount;
    [ObservableProperty] private bool _splitPerMap;

    public ObservableCollection<CollectionItemViewModel> Items { get; } = new();

    public Window? Owner { get; set; }

    public List<MapConfigFile>? GeneratedFiles { get; private set; }

    private readonly CollectionGenerateService _service = new();
    private CancellationTokenSource? _cts;

    [RelayCommand]
    private async Task FetchAsync()
    {
        var id = CollectionId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            StatusText = Localization.Get("Collection.NoId");
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsBusy = true;
        Items.Clear();
        HasItems = false;
        StatusText = Localization.Get("Collection.Fetching");

        try
        {
            var itemIds = await _service.FetchCollectionItemIdsAsync(id, ct);
            if (itemIds.Count == 0)
            {
                StatusText = Localization.Get("Collection.Empty");
                IsBusy = false;
                return;
            }

            StatusText = Localization.Format("Collection.FetchingDetails", itemIds.Count);
            var apiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();
            var progress = new Progress<(int done, int total)>(p =>
                StatusText = Localization.Format("Collection.Progress", p.done, p.total));

            var items = await _service.FetchItemDetailsAsync(itemIds, apiKey, progress, ct);

            foreach (var item in items)
                Items.Add(new CollectionItemViewModel(item.WorkshopId, item.Title, item.IsPublic));

            HasItems = Items.Count > 0;
            PublicCount = items.Count(i => i.IsPublic);
            NonPublicCount = items.Count(i => !i.IsPublic);
            StatusText = Localization.Format("Collection.Fetched", Items.Count, PublicCount, NonPublicCount);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText = Localization.Format("Collection.Error", ex.Message);
            Log.Error("Collection", $"Fetch failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (Items.Count == 0) return;
        if (Owner is null) return;

        var collectionItems = Items.Select(i => new CollectionItem(i.WorkshopId, i.Title, i.IsPublic)).ToList();

        if (SplitPerMap)
            await GeneratePerMapAsync(collectionItems);
        else
            await GenerateSingleFileAsync(collectionItems);
    }

    private async Task GenerateSingleFileAsync(List<CollectionItem> items)
    {
        var picked = await Owner!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Localization.Get("Collection.SaveTitle"),
            SuggestedFileName = $"collection_{CollectionId}.toml",
            DefaultExtension = "toml",
            FileTypeChoices = new[] { new FilePickerFileType("TOML") { Patterns = new[] { "*.toml" } } },
        });
        if (picked is null) return;
        var path = picked.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var file = _service.GenerateConfig(items, Path.GetFileName(path));
        file.FilePath = path;
        TomlConfigWriter.SaveFile(file);
        GeneratedFiles = [file];

        StatusText = Localization.Format("Collection.Generated", file.Maps.Count, path);
        Log.Info("Collection", $"Generated {file.Maps.Count} map(s) → {path}");
    }

    private async Task GeneratePerMapAsync(List<CollectionItem> items)
    {
        var folders = await Owner!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = Localization.Get("Collection.PickFolder"),
        });
        var folder = folders.FirstOrDefault();
        if (folder is null) return;
        var folderPath = folder.TryGetLocalPath();
        if (string.IsNullOrEmpty(folderPath)) return;

        Directory.CreateDirectory(folderPath);
        var files = new List<MapConfigFile>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var file = _service.GenerateConfig([item], $"{item.WorkshopId}.toml");
            var mapName = file.Maps[0].MapName;
            var fileName = mapName + ".toml";

            var candidate = fileName;
            var i = 2;
            while (usedNames.Contains(candidate))
                candidate = $"{mapName}_{i++}.toml";
            usedNames.Add(candidate);

            var path = Path.Combine(folderPath, candidate);
            file.FilePath = path;
            file.DisplayName = candidate;
            TomlConfigWriter.SaveFile(file);
            files.Add(file);
        }

        GeneratedFiles = files;
        StatusText = Localization.Format("Collection.GeneratedSplit", files.Count, folderPath);
        Log.Info("Collection", $"Generated {files.Count} file(s) → {folderPath}");
    }
}

public class CollectionItemViewModel
{
    public long WorkshopId { get; }
    public string Title { get; }
    public bool IsPublic { get; }
    public string StatusLabel => IsPublic ? "Public" : "Private/Unlisted";

    public CollectionItemViewModel(long workshopId, string title, bool isPublic)
    {
        WorkshopId = workshopId;
        Title = title;
        IsPublic = isPublic;
    }
}

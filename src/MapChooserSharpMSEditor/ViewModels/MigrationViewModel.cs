using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;
using MapChooserSharpMSEditor.Services.Legacy;

namespace MapChooserSharpMSEditor.ViewModels;

public partial class MigrationViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private bool _canMigrate;

    public ObservableCollection<MigrationFileViewModel> Files { get; } = new();

    public IReadOnlyList<string> RemovedProperties { get; }
    public IReadOnlyList<string> AddedProperties { get; }

    public Window? Owner { get; set; }

    public MigrationViewModel()
    {
        var (removed, added) = ConfigMigrationService.GetSchemaDiff();
        RemovedProperties = removed.ToList();
        AddedProperties = added.ToList();
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        if (Owner is null) return;
        var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = Localization.Get("Migration.PickSource"),
        });
        var folder = folders.FirstOrDefault();
        if (folder is null) return;
        var folderPath = folder.TryGetLocalPath();
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

        LoadLegacyFolder(folderPath);
    }

    private void LoadLegacyFolder(string folderPath)
    {
        Files.Clear();
        HasResults = false;

        string[] tomlFiles;
        try
        {
            tomlFiles = Directory.GetFiles(folderPath, "*.toml", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            StatusText = Localization.Format("Migration.Error", ex.Message);
            return;
        }

        if (tomlFiles.Length == 0)
        {
            StatusText = Localization.Get("Migration.NoFiles");
            return;
        }

        var skipped = 0;
        foreach (var path in tomlFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var legacy = LegacyConfigLoader.LoadFile(path);
                var result = ConfigMigrationService.Migrate(legacy);
                var removed = result.Changes.Where(c => c.Kind == MigrationChangeKind.Removed).ToList();

                Files.Add(new MigrationFileViewModel(
                    path,
                    Path.GetRelativePath(folderPath, path),
                    result,
                    removed));
            }
            catch (Exception ex)
            {
                skipped++;
                Log.Error("Migration", $"Failed to load {path}: {ex.Message}");
            }
        }

        HasResults = Files.Count > 0;
        if (string.IsNullOrEmpty(OutputFolder))
            OutputFolder = Path.Combine(Path.GetDirectoryName(folderPath) ?? folderPath, "migrated");
        UpdateCanMigrate();

        var status = Localization.Format("Migration.Loaded", Files.Count);
        if (skipped > 0)
            status += $" ({skipped} skipped)";
        StatusText = status;
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        if (Owner is null) return;
        var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = Localization.Get("Migration.PickOutput"),
        });
        var folder = folders.FirstOrDefault();
        if (folder is null) return;
        var folderPath = folder.TryGetLocalPath();
        if (!string.IsNullOrEmpty(folderPath))
        {
            OutputFolder = folderPath;
            UpdateCanMigrate();
        }
    }

    partial void OnOutputFolderChanged(string value) => UpdateCanMigrate();

    private void UpdateCanMigrate() => CanMigrate = HasResults && !string.IsNullOrWhiteSpace(OutputFolder);

    [RelayCommand]
    private void ExecuteMigration()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder)) return;

        try
        {
            Directory.CreateDirectory(OutputFolder);

            var count = 0;
            foreach (var fileVm in Files)
            {
                var outputPath = Path.Combine(OutputFolder, fileVm.RelativePath);
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                fileVm.Result.ConvertedFile.FilePath = outputPath;
                fileVm.Result.ConvertedFile.DisplayName = Path.GetFileName(outputPath);
                TomlConfigWriter.SaveFile(fileVm.Result.ConvertedFile);
                count++;
            }

            StatusText = Localization.Format("Migration.Done", count, OutputFolder);
            Log.Info("Migration", $"Migrated {count} file(s) to {OutputFolder}");
        }
        catch (Exception ex)
        {
            StatusText = Localization.Format("Migration.Error", ex.Message);
            Log.Error("Migration", $"Migration failed: {ex.Message}");
        }
    }
}

public class MigrationFileViewModel
{
    public string FullPath { get; }
    public string RelativePath { get; }
    public MigrationFileResult Result { get; }
    public IReadOnlyList<MigrationChange> RemovedChanges { get; }

    public int GroupCount => Result.ConvertedFile.Groups.Count;
    public int MapCount => Result.ConvertedFile.Maps.Count;
    public bool HasDefault => Result.ConvertedFile.DefaultSettings is not null;
    public int RemovedCount => RemovedChanges.Count;

    public MigrationFileViewModel(
        string fullPath,
        string relativePath,
        MigrationFileResult result,
        IReadOnlyList<MigrationChange> removedChanges)
    {
        FullPath = fullPath;
        RelativePath = relativePath;
        Result = result;
        RemovedChanges = removedChanges;
    }
}

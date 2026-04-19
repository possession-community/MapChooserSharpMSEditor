using MapChooserSharpMSEditor.Models;

namespace MapChooserSharpMSEditor.ViewModels.TreeNodes;

/// <summary>
/// A directory on disk. Contains <see cref="FolderNode"/> and <see cref="FileNode"/>
/// children mirroring the user's actual layout.
/// </summary>
public sealed class FolderNode : TreeNodeBase
{
    public string FolderPath { get; }
    public bool IsRoot { get; }

    public FolderNode(string path, bool isRoot = false)
    {
        FolderPath = path;
        IsRoot = isRoot;
        Icon = "\uE8B7";
        var name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        Title = string.IsNullOrEmpty(name) ? path : name;
    }
}

public sealed class FileNode : TreeNodeBase
{
    public MapConfigFile File { get; }

    public FileNode(MapConfigFile file)
    {
        File = file;
        Icon = "\uE8A5";
        Refresh();
        file.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MapConfigFile.DisplayName) or nameof(MapConfigFile.IsDirty))
                Refresh();
        };
    }

    public void Refresh()
    {
        Title = File.IsDirty ? $"{File.DisplayName} *" : File.DisplayName;
    }
}

public sealed class DefaultSettingsNode : TreeNodeBase
{
    public MapConfigFile File { get; }

    public DefaultSettingsNode(MapConfigFile file)
    {
        File = file;
        Icon = "\uE713";
        Title = "Default Settings";
    }
}

public sealed class CategoryNode : TreeNodeBase
{
    public CategoryKind Kind { get; }
    public MapConfigFile File { get; }

    public CategoryNode(MapConfigFile file, CategoryKind kind)
    {
        File = file;
        Kind = kind;
        Title = kind == CategoryKind.Groups ? "Groups" : "Maps";
        Icon = kind == CategoryKind.Groups ? "\uE902" : "\uE707";
    }
}

public enum CategoryKind { Groups, Maps }

public sealed class MapNode : TreeNodeBase
{
    public MapEntryModel Map { get; }
    public MapConfigFile File { get; }

    public MapNode(MapConfigFile file, MapEntryModel map)
    {
        File = file;
        Map = map;
        Icon = "\uE707";
        Refresh();
        map.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapEntryModel.MapName))
                Refresh();
        };
    }

    public void Refresh() => Title = Map.MapName;
}

public sealed class GroupNode : TreeNodeBase
{
    public GroupEntryModel Group { get; }
    public MapConfigFile File { get; }

    public GroupNode(MapConfigFile file, GroupEntryModel group)
    {
        File = file;
        Group = group;
        Icon = "\uE902";
        Refresh();
        group.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GroupEntryModel.GroupName))
                Refresh();
        };
    }

    public void Refresh() => Title = Group.GroupName;
}

public sealed class OverrideNode : TreeNodeBase
{
    public DaySettingsOverrideModel Override { get; }
    public MapConfigFile File { get; }
    public object Parent { get; }

    public OverrideNode(MapConfigFile file, object parent, DaySettingsOverrideModel ov)
    {
        File = file;
        Parent = parent;
        Override = ov;
        Icon = "\uE787";
        Refresh();
        ov.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DaySettingsOverrideModel.Name))
                Refresh();
        };
    }

    public void Refresh() => Title = Override.Name;
}

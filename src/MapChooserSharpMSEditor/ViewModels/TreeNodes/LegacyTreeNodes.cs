// LEGACY — remove when MCS migration completes
using MapChooserSharpMSEditor.Models.Legacy;

namespace MapChooserSharpMSEditor.ViewModels.TreeNodes;

public sealed class LegacyFileNode : TreeNodeBase
{
    public LegacyMapConfigFile File { get; }

    public LegacyFileNode(LegacyMapConfigFile file)
    {
        File = file;
        Icon = "\uE8A5";
        Refresh();
        file.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LegacyMapConfigFile.DisplayName) or nameof(LegacyMapConfigFile.IsDirty))
                Refresh();
        };
    }

    public void Refresh() => Title = File.IsDirty ? $"{File.DisplayName} *" : File.DisplayName;
}

public sealed class LegacyProjectDefaultNode : TreeNodeBase
{
    public LegacyProjectDefaultNode()
    {
        Icon = "\uE713";
        Title = "Default Settings";
    }
}

public sealed class LegacyCategoryNode : TreeNodeBase
{
    public CategoryKind Kind { get; }
    public LegacyMapConfigFile File { get; }

    public LegacyCategoryNode(LegacyMapConfigFile file, CategoryKind kind)
    {
        File = file;
        Kind = kind;
        Title = kind == CategoryKind.Groups ? "Groups" : "Maps";
        Icon = kind == CategoryKind.Groups ? "\uE902" : "\uE707";
    }
}

public sealed class LegacyMapNode : TreeNodeBase
{
    public LegacyMapEntry Map { get; }
    public LegacyMapConfigFile File { get; }

    public LegacyMapNode(LegacyMapConfigFile file, LegacyMapEntry map)
    {
        File = file;
        Map = map;
        Icon = "\uE707";
        Refresh();
        map.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LegacyMapEntry.MapName))
                Refresh();
        };
    }

    public void Refresh() => Title = Map.MapName;
}

public sealed class LegacyGroupNode : TreeNodeBase
{
    public LegacyGroupEntry Group { get; }
    public LegacyMapConfigFile File { get; }

    public LegacyGroupNode(LegacyMapConfigFile file, LegacyGroupEntry group)
    {
        File = file;
        Group = group;
        Icon = "\uE902";
        Refresh();
        group.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LegacyGroupEntry.GroupName))
                Refresh();
        };
    }

    public void Refresh() => Title = Group.GroupName;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MapChooserSharpMSEditor.Models;

public partial class ExtraKeyValue : ObservableObject
{
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private ExtraValueKind _kind = ExtraValueKind.String;
}

public enum ExtraValueKind
{
    String,
    Integer,
    Float,
    Boolean,
}

public partial class ExtraSection : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;

    /// <summary>
    /// Observable so that adding a new key via the UI triggers a re-render. Also lets the
    /// undo system record individual key insertions/removals.
    /// </summary>
    public ObservableCollection<ExtraKeyValue> Entries { get; } = new();
}

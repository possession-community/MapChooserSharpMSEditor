// LEGACY — remove when MCS migration completes
using Avalonia.Controls;

namespace MapChooserSharpMSEditor.Views.Legacy;

public partial class LegacySearchWindow : Window
{
    public LegacySearchWindow() => InitializeComponent();

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        this.FindControl<TextBox>("QueryBox")?.Focus();
    }
}

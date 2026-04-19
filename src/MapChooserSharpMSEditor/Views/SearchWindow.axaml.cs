using Avalonia.Controls;
using Avalonia.Input;

namespace MapChooserSharpMSEditor.Views;

public partial class SearchWindow : Window
{
    public SearchWindow() => InitializeComponent();

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        // Put cursor straight into the query box so the user can just start typing.
        this.FindControl<TextBox>("QueryBox")?.Focus();
    }
}

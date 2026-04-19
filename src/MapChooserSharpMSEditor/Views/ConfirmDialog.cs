using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MapChooserSharpMSEditor.Views;

/// <summary>
/// A tiny modal yes/no confirmation dialog. Rolled inline instead of pulling in a
/// full MessageBox package — the editor only needs one of these.
/// </summary>
public static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message, string yesText, string noText)
    {
        var tcs = new TaskCompletionSource<bool>();

        var yes = new Button { Content = yesText, IsDefault = true, MinWidth = 100 };
        var no = new Button { Content = noText, IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#1c1c1c"),
        };

        yes.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        no.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { yes, no },
                },
            },
        };

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}

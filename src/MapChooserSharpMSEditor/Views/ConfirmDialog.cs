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
            MaxHeight = 500,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#1c1c1c"),
        };

        yes.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        no.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        var panel = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { yes, no },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 380,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13 },
        });
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }

    public enum TriResult { Primary, Secondary, Cancel }

    /// <summary>
    /// Three-button variant for choices that aren't yes/no — e.g. schema mismatch
    /// ("switch mode" / "open anyway" / "cancel"). Primary is the default button.
    /// </summary>
    public static async Task<TriResult> ShowTriAsync(
        Window owner, string title, string message,
        string primaryText, string secondaryText, string cancelText)
    {
        var tcs = new TaskCompletionSource<TriResult>();

        var primary = new Button { Content = primaryText, IsDefault = true, MinWidth = 110 };
        var secondary = new Button { Content = secondaryText, MinWidth = 110, Margin = new Thickness(8, 0, 0, 0) };
        var cancel = new Button { Content = cancelText, IsCancel = true, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };

        var dialog = new Window
        {
            Title = title,
            Width = 480,
            MaxHeight = 500,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#1c1c1c"),
        };

        primary.Click += (_, _) => { tcs.TrySetResult(TriResult.Primary); dialog.Close(); };
        secondary.Click += (_, _) => { tcs.TrySetResult(TriResult.Secondary); dialog.Close(); };
        cancel.Click += (_, _) => { tcs.TrySetResult(TriResult.Cancel); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(TriResult.Cancel);

        var panel = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { primary, secondary, cancel },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 380,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13 },
        });
        dialog.Content = panel;

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}

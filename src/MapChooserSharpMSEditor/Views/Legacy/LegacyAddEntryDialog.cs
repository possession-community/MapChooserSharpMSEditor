// LEGACY — remove when MCS migration completes
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MapChooserSharpMSEditor.Models.Legacy;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Views.Legacy;

public enum LegacyAddEntryKind { Map, Group }

public sealed record LegacyAddEntryResult(LegacyMapConfigFile Target, string Name);

public static class LegacyAddEntryDialog
{
    private sealed record FileChoice(LegacyMapConfigFile File, string Label)
    {
        public override string ToString() => Label;
    }

    public static async Task<LegacyAddEntryResult?> ShowAsync(
        Window owner,
        LegacyAddEntryKind kind,
        IReadOnlyList<LegacyMapConfigFile> files,
        LegacyMapConfigFile? current,
        string defaultName,
        Func<Task<LegacyMapConfigFile?>> newFileFactory)
    {
        var tcs = new TaskCompletionSource<LegacyAddEntryResult?>();

        var nameBox = new TextBox
        {
            Text = defaultName,
            Watermark = Localization.Get(kind == LegacyAddEntryKind.Map ? "AddEntry.Map.NameWatermark" : "AddEntry.Group.NameWatermark"),
            MinWidth = 260,
        };

        var rbCurrent = new RadioButton
        {
            GroupName = "ltarget",
            Content = current is not null
                ? new TextBlock { Text = string.Format(Localization.Get("AddEntry.Target.Current"), current.DisplayName) }
                : new TextBlock { Text = Localization.Get("AddEntry.Target.CurrentNone") },
            IsEnabled = current is not null,
        };

        var choices = files.Select(f => new FileChoice(f, f.DisplayName)).ToList();
        var autoBox = new AutoCompleteBox
        {
            ItemsSource = choices,
            FilterMode = AutoCompleteFilterMode.Contains,
            MinimumPrefixLength = 0,
            MinWidth = 260,
            IsEnabled = false,
            Watermark = Localization.Get("AddEntry.Target.ExistingWatermark"),
        };
        autoBox.GotFocus += (_, _) => autoBox.IsDropDownOpen = true;
        var rbExisting = new RadioButton
        {
            GroupName = "ltarget",
            Content = new TextBlock { Text = Localization.Get("AddEntry.Target.Existing") },
            IsEnabled = choices.Count > 0,
        };
        rbExisting.IsCheckedChanged += (_, _) => autoBox.IsEnabled = rbExisting.IsChecked == true;

        var rbNew = new RadioButton
        {
            GroupName = "ltarget",
            Content = new TextBlock { Text = Localization.Get("AddEntry.Target.New") },
        };

        if (current is not null) rbCurrent.IsChecked = true;
        else rbNew.IsChecked = true;

        var ok = new Button { Content = Localization.Get("Button.OK"), IsDefault = true, MinWidth = 90 };
        var cancel = new Button { Content = Localization.Get("Button.Cancel"), IsCancel = true, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };

        var dialog = new Window
        {
            Title = Localization.Get(kind == LegacyAddEntryKind.Map ? "AddEntry.Map.Title" : "AddEntry.Group.Title"),
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#1c1c1c"),
        };

        ok.Click += async (_, _) =>
        {
            var name = nameBox.Text?.Trim();
            if (string.IsNullOrEmpty(name)) { nameBox.Focus(); return; }

            LegacyMapConfigFile? target = null;
            if (rbCurrent.IsChecked == true)
            {
                target = current;
            }
            else if (rbExisting.IsChecked == true)
            {
                target = (autoBox.SelectedItem as FileChoice)?.File
                    ?? choices.FirstOrDefault(c => string.Equals(c.Label, autoBox.Text, StringComparison.Ordinal))?.File;
                if (target is null) { autoBox.Focus(); return; }
            }
            else if (rbNew.IsChecked == true)
            {
                target = await newFileFactory();
                if (target is null) return;
            }

            if (target is null) return;
            tcs.TrySetResult(new LegacyAddEntryResult(target, name));
            dialog.Close();
        };
        cancel.Click += (_, _) => { tcs.TrySetResult(null); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(null);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Localization.Get("AddEntry.Name"), Foreground = Brushes.LightGray, FontSize = 12 },
                nameBox,
                new TextBlock
                {
                    Text = Localization.Get("AddEntry.Target"),
                    Foreground = Brushes.LightGray,
                    FontSize = 12,
                    Margin = new Thickness(0, 6, 0, 0),
                },
                rbCurrent,
                rbExisting,
                new StackPanel { Margin = new Thickness(24, 0, 0, 0), Children = { autoBox } },
                rbNew,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 8, 0, 0),
                    Children = { ok, cancel },
                },
            },
        };

        nameBox.Loaded += (_, _) => nameBox.Focus();

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}

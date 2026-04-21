using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MapChooserSharpMSEditor.Models;
using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.Views;

public enum AddEntryKind { Map, Group }

/// <summary>Result of the Add-Map/Group dialog. Null return = user cancelled.</summary>
public sealed record AddEntryResult(MapConfigFile Target, string Name);

/// <summary>
/// Small modal that asks the user whether the new Map/Group should go into the currently
/// selected file, some other already-loaded file, or a brand-new file. Written in code
/// (no AXAML) to match the sibling <see cref="ConfirmDialog"/> style.
///
/// <para>Underscores in labels are wrapped in TextBlocks instead of set as raw string
/// <c>Content</c> so the control doesn't treat <c>_</c> as an access-key mnemonic and
/// drop the character from the visible label.</para>
/// </summary>
public static class AddEntryDialog
{
    /// <summary>
    /// Wraps a file with a display label so AutoCompleteBox can filter by name while still
    /// giving the dialog the original <see cref="MapConfigFile"/> back on selection.
    /// </summary>
    private sealed record FileChoice(MapConfigFile File, string Label)
    {
        public override string ToString() => Label;
    }

    public static async Task<AddEntryResult?> ShowAsync(
        Window owner,
        AddEntryKind kind,
        IReadOnlyList<MapConfigFile> files,
        MapConfigFile? current,
        string defaultName,
        Func<Task<MapConfigFile?>> newFileFactory)
    {
        var tcs = new TaskCompletionSource<AddEntryResult?>();

        // ----- Name -----
        var nameBox = new TextBox
        {
            Text = defaultName,
            Watermark = Localization.Get(kind == AddEntryKind.Map ? "AddEntry.Map.NameWatermark" : "AddEntry.Group.NameWatermark"),
            MinWidth = 260,
        };

        // ----- Target radios -----
        var rbCurrent = new RadioButton
        {
            GroupName = "target",
            Content = current is not null
                ? new TextBlock { Text = string.Format(Localization.Get("AddEntry.Target.Current"), current.DisplayName) }
                : new TextBlock { Text = Localization.Get("AddEntry.Target.CurrentNone") },
            IsEnabled = current is not null,
        };

        // Every loaded file is selectable from here, including the current one. Users sometimes
        // want to verify they're picking the same file they already have selected, so hiding
        // it from the list caused confusion ("why aren't all my configs listed?").
        var choices = files.Select(f => new FileChoice(f, f.DisplayName)).ToList();
        var autoBox = new AutoCompleteBox
        {
            ItemsSource = choices,
            FilterMode = AutoCompleteFilterMode.Contains,
            // MinimumPrefixLength=0 lets an empty query match every item, so focus alone is
            // enough to surface the whole loaded-file list and the user can narrow from there.
            MinimumPrefixLength = 0,
            MinWidth = 260,
            IsEnabled = false,
            Watermark = Localization.Get("AddEntry.Target.ExistingWatermark"),
        };
        // Pop the dropdown immediately on focus so the user sees what's available without
        // having to type a starter character first.
        autoBox.GotFocus += (_, _) => autoBox.IsDropDownOpen = true;
        var rbExisting = new RadioButton
        {
            GroupName = "target",
            Content = new TextBlock { Text = Localization.Get("AddEntry.Target.Existing") },
            IsEnabled = choices.Count > 0,
        };
        rbExisting.IsCheckedChanged += (_, _) => autoBox.IsEnabled = rbExisting.IsChecked == true;

        var rbNew = new RadioButton
        {
            GroupName = "target",
            Content = new TextBlock { Text = Localization.Get("AddEntry.Target.New") },
        };

        // Default pick: current if we have one, otherwise the new-file path so the user
        // isn't stuck with no option to proceed.
        if (current is not null) rbCurrent.IsChecked = true;
        else rbNew.IsChecked = true;

        // ----- Buttons -----
        var ok = new Button
        {
            Content = Localization.Get("Button.OK"),
            IsDefault = true,
            MinWidth = 90,
        };
        var cancel = new Button
        {
            Content = Localization.Get("Button.Cancel"),
            IsCancel = true,
            MinWidth = 90,
            Margin = new Thickness(8, 0, 0, 0),
        };

        // ----- Window -----
        var dialog = new Window
        {
            Title = Localization.Get(kind == AddEntryKind.Map ? "AddEntry.Map.Title" : "AddEntry.Group.Title"),
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

            MapConfigFile? target = null;
            if (rbCurrent.IsChecked == true)
            {
                target = current;
            }
            else if (rbExisting.IsChecked == true)
            {
                // Accept either an explicit selection or a typed-in exact match so the
                // AutoCompleteBox behaves like an Enter-to-commit combo too.
                target = (autoBox.SelectedItem as FileChoice)?.File
                    ?? choices.FirstOrDefault(c => string.Equals(c.Label, autoBox.Text, StringComparison.Ordinal))?.File;
                if (target is null) { autoBox.Focus(); return; }
            }
            else if (rbNew.IsChecked == true)
            {
                // Factory prompts for a save path — cancel keeps the dialog open so the user
                // can pick a different target rather than losing the name they already typed.
                target = await newFileFactory();
                if (target is null) return;
            }

            if (target is null) return;

            tcs.TrySetResult(new AddEntryResult(target, name));
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

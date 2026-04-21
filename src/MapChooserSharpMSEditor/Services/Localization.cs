using System;
using System.Collections.Generic;
using System.Globalization;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Minimal string catalog used throughout the UI. Keys are dotted namespaces.
///
/// To add a locale, add another entry to <see cref="_strings"/>. Missing keys fall back to
/// English, which falls back to the raw key so developers can spot missing entries.
///
/// Current limitation: <see cref="CurrentLocale"/> is read once at startup and injected into
/// the markup extension's ProvideValue. Changing it at runtime does not re-resolve already-
/// rendered strings.
/// </summary>
public static class Localization
{
    public const string DefaultLocale = "en";

    public static string CurrentLocale { get; set; } = DefaultLocale;

    /// <summary>Locales the UI explicitly ships translations for.</summary>
    public static readonly LocaleOption[] AvailableLocales =
    {
        new("en", "English"),
        new("ja", "日本語"),
    };

    static Localization()
    {
        // Priority: explicit user choice → OS locale → "en".
        if (!string.IsNullOrEmpty(UserSettings.Locale))
        {
            CurrentLocale = UserSettings.Locale!;
            return;
        }
        var ui = CultureInfo.CurrentUICulture;
        if (ui.TwoLetterISOLanguageName == "ja")
            CurrentLocale = "ja";
    }

    public static string Get(string key, string? fallback = null)
    {
        if (_strings.TryGetValue(CurrentLocale, out var cur) && cur.TryGetValue(key, out var val))
            return val;
        if (CurrentLocale != DefaultLocale
            && _strings.TryGetValue(DefaultLocale, out var def)
            && def.TryGetValue(key, out var defVal))
            return defVal;
        return fallback ?? key;
    }

    /// <summary>Shortcut for <c>string.Format(Get(key), args)</c>.</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(Get(key), args);

    private static readonly Dictionary<string, Dictionary<string, string>> _strings = new()
    {
        ["en"] = new()
        {
            // ===== App =====
            ["App.Title"] = "MapChooserSharpMS Config Editor",

            // ===== Menu =====
            ["Menu.File"] = "_File",
            ["Menu.File.New"] = "_New",
            ["Menu.File.OpenFile"] = "_Open File...",
            ["Menu.File.OpenFolder"] = "Open _Folder...",
            ["Menu.File.Save"] = "_Save",
            ["Menu.File.SaveAs"] = "Save _As...",
            ["Menu.File.SaveAll"] = "Save A_ll",
            ["Menu.File.Close"] = "_Close File",
            ["Menu.File.CloseAll"] = "Clos_e All",
            ["Confirm.CloseAll.Title"] = "Close every loaded file",
            ["Confirm.CloseAll.Message"] = "The following file(s) have unsaved changes and will be lost:\n  • {0}\n\nProceed?",
            ["Confirm.CloseAll.Yes"] = "Close all",
            ["Confirm.CloseAll.No"] = "Cancel",
            ["Menu.Edit"] = "_Edit",
            ["Menu.Edit.Undo"] = "_Undo",
            ["Menu.Edit.Redo"] = "_Redo",
            ["Menu.Edit.AddMap"] = "Add _Map",
            ["Menu.Edit.AddGroup"] = "Add _Group",
            ["Menu.Edit.Find"] = "_Find...",
            ["Menu.Tools"] = "_Tools",
            ["Menu.Tools.WorkshopCheck"] = "Check _Workshop items...",
            ["Menu.View"] = "_View",
            ["Menu.View.Language"] = "_Language",
            ["Menu.View.DebugConsole"] = "_Debug Console",
            ["Debug.Header"] = "Debug Console",
            ["Debug.Clear"] = "Clear",

            // ===== Workshop check dialog =====
            ["WorkshopCheck.Title"] = "Workshop Item Status Check",
            ["WorkshopCheck.Intro"] = "Looks up every loaded map's WorkshopId against Steam and lists ones that are no longer public (deleted / private / unlisted) and aren't already marked Disabled. Pick which ones to flip.",
            ["WorkshopCheck.Run"] = "Run check",
            ["WorkshopCheck.Running"] = "Checking {0} / {1}...",
            ["WorkshopCheck.NoTargets"] = "No maps with WorkshopId to check (all are either unset or already Disabled).",
            ["WorkshopCheck.AllPublic"] = "All checked items are still public.",
            ["WorkshopCheck.FoundNonPublic"] = "Found {0} item(s) that are no longer public.",
            ["WorkshopCheck.Failed"] = "Check failed: {0}",
            ["WorkshopCheck.NoIssues"] = "No issues to report. Run a check to start.",
            ["WorkshopCheck.ApplySelected"] = "Mark selected as Disabled",
            ["WorkshopCheck.SelectAll"] = "Select all",
            ["WorkshopCheck.SelectNone"] = "Select none",
            ["WorkshopCheck.Applied"] = "Marked {0} map(s) as Disabled.",

            // ===== Workspace warning banner =====
            ["Banner.SectionCollisions"] = "Duplicate section(s) detected — the server's TOML parser will reject this config. Keep exactly one copy of each path below.",

            // ===== Tree context menu =====
            ["ContextMenu.RemoveFromView"] = "Remove from view",

            // ===== Discard-dirty confirmation =====
            ["Confirm.DiscardDirty.Title"] = "Unsaved changes",
            ["Confirm.DiscardDirty.Message"] = "The following file(s) have unsaved changes and will be lost:\n  • {0}\n\nProceed anyway?",
            ["Confirm.DiscardDirty.Yes"] = "Discard",
            ["Confirm.DiscardDirty.No"] = "Cancel",

            // ===== Add Map / Group dialog =====
            ["AddEntry.Map.Title"] = "Add Map",
            ["AddEntry.Group.Title"] = "Add Group",
            ["AddEntry.Name"] = "Name",
            ["AddEntry.Map.NameWatermark"] = "e.g. de_mirage",
            ["AddEntry.Group.NameWatermark"] = "e.g. HardZeMaps",
            ["AddEntry.Target"] = "Add to",
            ["AddEntry.Target.Current"] = "Current file ({0})",
            ["AddEntry.Target.CurrentNone"] = "Current file (none selected)",
            ["AddEntry.Target.Existing"] = "Another loaded file",
            ["AddEntry.Target.ExistingWatermark"] = "Type to filter files...",
            ["AddEntry.Target.New"] = "New file (choose save location)",
            ["AddEntry.NewFile.PickerTitle"] = "Choose location for the new TOML file",

            // ===== Restart confirmation =====
            ["Restart.Title"] = "Restart Required",
            ["Restart.Message"] = "Restart the app now to apply the language change?",
            ["Restart.Yes"] = "Restart",
            ["Restart.No"] = "Later",

            // ===== Sidebar =====
            ["Sidebar.Header"] = "Config Files",
            ["Sidebar.ToggleResolved"] = "Toggle resolved-values panel",

            // ===== Search =====
            ["Search.Title"] = "Search",
            ["Search.Watermark"] = "Search maps, groups, overrides by name / alias / group name",
            ["Search.NoResults"] = "No matches",
            ["Search.Filter.Maps"] = "Maps",
            ["Search.Filter.Groups"] = "Groups",
            ["Search.Filter.Overrides"] = "Overrides",
            ["Search.Filter.Defaults"] = "Defaults",
            ["Search.Kind.Default"] = "Default",
            ["Search.Kind.Group"] = "Group",
            ["Search.Kind.Map"] = "Map",
            ["Search.Kind.Override"] = "Override",
            ["Search.AdvancedToggle"] = "Advanced filters",
            ["Search.Advanced.Intro"] = "Fill any combination of filters. Empty fields and \"Any\" ops are skipped; the rest AND together with the main text search.",
            ["Search.Advanced.Reset"] = "Reset",
            ["Search.Advanced.Group"] = "Group",
            ["Search.Advanced.File"] = "File",
            ["Search.Advanced.Alias"] = "Alias",
            ["Search.Advanced.Desc"] = "Description",
            ["Search.Advanced.Cooldown"] = "Cooldown",
            ["Search.Advanced.Workshop"] = "Workshop ID",
            ["Search.Advanced.MaxPlayers"] = "MaxPlayers",
            ["Search.Advanced.MinPlayers"] = "MinPlayers",
            ["Search.Advanced.MapTime"] = "MapTime (min)",
            ["Search.Advanced.MapRounds"] = "MapRounds",
            ["Search.Advanced.Disabled"] = "Disabled",
            ["Search.Advanced.OnlyNom"] = "OnlyNomination",
            ["Search.Advanced.ProhibitAdmin"] = "ProhibitAdmin",
            ["Search.Advanced.Day"] = "Day",
            ["Search.Advanced.ExtrasKey"] = "Extras key",
            ["Search.Advanced.ExtrasValue"] = "Extras value",
            ["Search.Help.Intro"] = "Use plain words for a name/alias match, or field:value filters for precise queries. All filters are AND. Quote values with spaces.",
            ["Search.Help.OpsNote"] = "Numeric ops: = (default), >, >=, <, <=.  Booleans: true/false/yes/no/on/off.",
            ["Search.Help.Name"] = "Matches the candidate's name, alias, group/map title.",
            ["Search.Help.Alias"] = "Matches MapNameAlias (display name) only.",
            ["Search.Help.Desc"] = "Matches MapDescription only.",
            ["Search.Help.File"] = "Matches the containing TOML filename.",
            ["Search.Help.Kind"] = "Restrict by shape (same as the filter chips).",
            ["Search.Help.Group"] = "Group's own name, or Maps/Overrides that inherit from a group with this name.",
            ["Search.Help.Workshop"] = "WorkshopId equals the number.",
            ["Search.Help.Cooldown"] = "Cooldown (plays) compared numerically. e.g. cooldown:>30",
            ["Search.Help.MaxPlayers"] = "MaxPlayers compared numerically.",
            ["Search.Help.MinPlayers"] = "MinPlayers compared numerically.",
            ["Search.Help.MapTime"] = "MapTime (minutes) compared numerically.",
            ["Search.Help.MapRounds"] = "MapRounds compared numerically.",
            ["Search.Help.MaxExtends"] = "MaxExtends compared numerically.",
            ["Search.Help.Disabled"] = "IsDisabled flag (true/false).",
            ["Search.Help.OnlyNom"] = "OnlyNomination flag (true/false).",
            ["Search.Help.Nominate"] = "Inverse of OnlyNomination — true means nominate-eligible.",
            ["Search.Help.ProhibitAdmin"] = "ProhibitAdminNomination flag (true/false).",
            ["Search.Help.Day"] = "Matches one of DaysAllowed. Full name or prefix (e.g. day:mon).",
            ["Search.Help.Extra"] = "Extras entry: key or key=value; section.key=value also accepted.",
            ["Search.Help.ExtraSection"] = "Has an Extras sub-section whose name contains the value.",

            // ===== Welcome =====
            ["Welcome.Title"] = "MapChooserSharpMS Config Editor",
            ["Welcome.Description"] = "Use File → Open File / Open Folder to load a TOML config.\nSelect Default / Groups / Maps / DaySettings in the left tree to edit.",

            // ===== Section headers =====
            ["Section.Basic"] = "Basic",
            ["Section.ExtendTime"] = "Extend / Time",
            ["Section.Nomination"] = "Nomination / Pick",
            ["Section.Cooldown"] = "Cooldown",
            ["Section.Extra"] = "Extra (external plugin data)",
            ["Section.Basic.Desc"] = "Per-map / per-group identity and gating. Display name, Steam Workshop ID, whether the entry is disabled for everyone, and (for maps) which groups it inherits settings from.",
            ["Section.ExtendTime.Desc"] = "How long the map runs and how the !extend command behaves on it. MapTime / MapRounds seed mp_timelimit / mp_maxrounds; the Max/Time/Rounds-per-extend set extend quotas.",
            ["Section.Nomination.Desc"] = "Who can pick this map and when. OnlyNomination excludes from random picks; Max/MinPlayers gate by player count; ProhibitAdminNomination locks picks to the server console; DaysAllowed / AllowedTimeRanges restrict by schedule.",
            ["Section.Cooldown.Desc"] = "Controls how long the server waits before allowing this map to be selected again. Cooldown counts map plays; CooldownDateTime uses real time (e.g. 2d / 1m).",
            ["Section.Extra.Desc"] = "Free-form sub-tables ([extra.section]) that external plugins read at runtime. The editor preserves keys and values verbatim — type interpretation is the consuming plugin's responsibility.",

            // ===== Common buttons / actions =====
            ["Button.Reset"] = "Reset",
            ["Button.ResetAll"] = "Reset all",
            ["Button.OK"] = "OK",
            ["Button.Cancel"] = "Cancel",
            ["Button.Add"] = "Add",
            ["Button.AddEntry"] = "+ Add",
            ["Button.Remove"] = "Remove",
            ["Button.AddSection"] = "Add Section",
            ["Button.RemoveSection"] = "Remove Section",
            ["Button.AddKey"] = "+ key",
            ["Button.Open"] = "Open",
            ["Button.OpenDefault"] = "Open Default Settings",
            ["Button.AddOverride"] = "+ Add Override",

            // ===== Status / inline annotations =====
            ["Status.UsingDefault"] = "using default",
            ["Status.NoInheritance"] = "no inheritance",
            ["Status.NoOverride"] = "no override",
            ["Status.FromOverrideTarget"] = "from override target",
            ["Status.Ready"] = "Ready",

            // ===== Resolved-panel sources =====
            ["Source.OverrideTargetMap"] = "Override target (Map: {0})",
            ["Source.OverrideTargetGroup"] = "Override target (Group: {0})",
            ["Source.Unset"] = "(unset)",
            ["Source.Default"] = "Default",
            ["Source.DefaultFromFile"] = "Default ({0})",
            ["Source.Group"] = "Group: {0}",
            ["Source.Map"] = "Map: {0}",
            ["Source.Override"] = "Override: {0}",
            ["Source.GroupCooldownOverride"] = "Group: {0} (CooldownOverride)",

            // ===== Tooltips =====
            ["Tip.InvalidTimeRange"] = "Expected HH:mm-HH:mm (e.g. 19:00-23:30). Overnight ranges supported.",
            ["Tip.Reset"] = "Revert to default (removes entry from TOML)",
            ["Tip.ResetAll"] = "Clear all entries and revert to default",
            ["Tip.UnknownGroup"] = "This group name isn't defined in any loaded file.",

            // ===== Watermarks =====
            ["Watermark.GroupName"] = "group name",
            ["Watermark.SectionName"] = "section name (e.g. shop)",
            ["Watermark.TimeRange"] = "HH:mm-HH:mm",
            ["Watermark.ExtraKey"] = "key",
            ["Watermark.ExtraValue"] = "value",
            ["Watermark.Filter"] = "Filter groups and maps...",
            ["Watermark.Search"] = "Search by name...",
            ["Watermark.CooldownDateTime"] = "e.g. 2d or 1m",

            // ===== File overview / category list =====
            ["Overview.GroupsCount"] = "Groups ({0})",
            ["Overview.MapsCount"] = "Maps ({0})",
            ["Overview.NoGroups"] = "(no groups)",
            ["Overview.NoMaps"] = "(no maps)",
            ["Overview.DaySettingsCount"] = "{0} day-settings",
            ["Overview.MapAliasFormat"] = "\u201C{0}\u201D",

            ["Category.Maps"] = "Maps",
            ["Category.Groups"] = "Groups",
            ["Category.EmptyMaps"] = "No maps are defined in this file yet.",
            ["Category.EmptyGroups"] = "No groups are defined in this file yet.",

            // ===== File line in editor headers =====
            ["Label.FilePrefix"] = "File: {0}",

            // ===== Map / Group editor headers =====
            ["Editor.Map"] = "Map:",
            ["Editor.Group"] = "Group:",
            ["Editor.Override"] = "Override:",
            ["Editor.ParentMap"] = "Map: {0}",
            ["Editor.ParentGroup"] = "Group: {0}",

            // ===== DaySettings section =====
            ["DaySettings.Title"] = "DaySettings Overrides",
            ["DaySettings.GroupTitle"] = "DaySettings Overrides (applies to all maps referencing this group)",
            ["DaySettings.DaysLabel"] = "Days: {0}",
            ["DaySettings.TimesLabel"] = "Times: {0}",
            ["DaySettings.PriorityLabel"] = "Priority: {0}",
            ["DaySettings.EditHint"] = "To edit a DaySettings override, click its name in the tree under this Map.",

            // ===== Override editor: trigger block =====
            ["Override.Trigger"] = "Trigger",
            ["Override.Enabled"] = "Enabled",
            ["Override.ForceOverride"] = "ForceOverride",
            ["Override.OverridePriority"] = "OverridePriority",
            ["Override.TargetDays"] = "TargetDays",
            ["Override.TargetTimeRanges"] = "TargetTimeRanges",
            ["Override.SelectedDays"] = "selected: {0}",
            ["Override.OverriddenProps"] = "Overridden Properties (only checked fields are written)",

            // ===== Default settings header =====
            ["Default.Title"] = "Default Settings",
            ["Default.Section"] = "[MapChooserSharpSettings.Default]",
            ["Default.OwnerLabel"] = "Stored in: {0}",
            ["Default.RemoveOwner"] = "Detach from this file",
            ["Default.RemoveOwner.Tip"] = "Clears the Default section from this file. Re-assign it to another file afterwards.",
            ["Default.NoOwner.Title"] = "No Default section in this project yet",
            ["Default.NoOwner.Body"] = "The server requires exactly one [MapChooserSharpSettings.Default] section across all loaded files. Pick which file should own it.",
            ["Default.Assign"] = "Assign to file",
            ["Default.MultipleWarning.Title"] = "Multiple Default sections detected",
            ["Default.MultipleWarning.Body"] = "The server's TOML parser rejects duplicate sections. This project won't load until all but one of the files below has its Default removed.",

            // ===== Resolved panel =====
            ["Resolved.Placeholder.Heading"] = "Resolved Values",
            ["Resolved.Placeholder.Body"] = "Select a map, group or override to preview.",
            ["Resolved.HeadingDefault"] = "Effective Values \u2014 Default",
            ["Resolved.HeadingGroup"] = "Effective Values \u2014 Group: {0}",
            ["Resolved.HeadingMap"] = "Effective Values \u2014 Map: {0}",
            ["Resolved.HeadingOverride"] = "Effective Values \u2014 Override: {0}",
            ["Resolved.ExtrasHeading"] = "Extras",
            ["Resolved.Extras.Key"] = "Key",
            ["Resolved.Extras.Value"] = "Value",
            ["Resolved.Extras.Type"] = "Type",
            ["Resolved.Extras.Source"] = "Source",

            // ===== Status bar messages =====
            ["Status.Loaded"] = "Loaded {0}",
            ["Status.LoadFailed"] = "Failed to load {0}: {1}",
            ["Status.OpenedFolder"] = "Opened folder {0}",
            ["Status.NoTomlFound"] = "No .toml files found under {0}",
            ["Status.Skipped"] = "Skipped {0}: {1}",
            ["Status.Saved"] = "Saved {0}",
            ["Status.SaveFailed"] = "Save failed: {0}",

            // ===== Extra markers =====
            ["Extra.Prefix"] = "[extra.",

            // ===== Property labels =====
            ["Prop.MapNameAlias"] = "Display Name",
            ["Prop.MapDescription"] = "Description",
            ["Prop.WorkshopId"] = "Workshop ID",
            ["Prop.IsDisabled"] = "Disabled",
            ["Prop.GroupSettings"] = "Inherit Groups",
            ["Prop.CooldownOverride"] = "Cooldown Override (group-only)",
            ["Prop.MaxExtends"] = "Max Extends",
            ["Prop.MaxExtCommandUses"] = "Max !ext Uses",
            ["Prop.ExtendTimePerExtends"] = "Time per Extend (min)",
            ["Prop.MapTime"] = "Map Time (min)",
            ["Prop.ExtendRoundsPerExtends"] = "Rounds per Extend",
            ["Prop.MapRounds"] = "Map Rounds",
            ["Prop.OnlyNomination"] = "Only Nomination",
            ["Prop.MaxPlayers"] = "Max Players",
            ["Prop.MinPlayers"] = "Min Players",
            ["Prop.ProhibitAdminNomination"] = "Prohibit Admin Nomination",
            ["Prop.DaysAllowed"] = "Allowed Days",
            ["Prop.AllowedTimeRanges"] = "Allowed Time Ranges",
            ["Prop.Cooldown"] = "Cooldown (plays)",
            ["Prop.CooldownDateTime"] = "Cooldown (time)",

            // ===== Property descriptions (tooltips) =====
            ["Prop.MapNameAlias.Desc"] = "Display name shown in place of the raw map name.",
            ["Prop.MapDescription.Desc"] = "Shown after the vote finishes, before the map transition.",
            ["Prop.WorkshopId.Desc"] = "Steam Workshop ID for workshop maps.",
            ["Prop.IsDisabled.Desc"] = "If set, the map cannot be nominated even by admin. Also excludes the map from Random Map Pick.",
            ["Prop.GroupSettings.Desc"] = "Groups whose settings this map inherits. First group wins on conflicts.",
            ["Prop.CooldownOverride.Desc"] = "Overrides Cooldown for every map that references this group. Group-only; ignored elsewhere.",
            ["Prop.MaxExtends.Desc"] = "Maximum number of extends allowed on this map.",
            ["Prop.MaxExtCommandUses.Desc"] = "Maximum times the !ext command can be invoked.",
            ["Prop.ExtendTimePerExtends.Desc"] = "Minutes added to mp_timelimit per extend.",
            ["Prop.MapTime.Desc"] = "Initial mp_timelimit value (minutes).",
            ["Prop.ExtendRoundsPerExtends.Desc"] = "Rounds added per extend (round-based cycle).",
            ["Prop.MapRounds.Desc"] = "Initial mp_maxrounds value.",
            ["Prop.OnlyNomination.Desc"] = "Excludes the map from Random Map Pick — it can only be chosen via nomination.",
            ["Prop.MaxPlayers.Desc"] = "Nomination and Random Map Pick allowed only when player count \u2264 this.",
            ["Prop.MinPlayers.Desc"] = "Nomination and Random Map Pick allowed only when player count \u2265 this.",
            ["Prop.ProhibitAdminNomination.Desc"] = "Only the server console can nominate. Does not affect Random Map Pick.",
            ["Prop.DaysAllowed.Desc"] = "Nomination and Random Map Pick restricted to these days of week.",
            ["Prop.AllowedTimeRanges.Desc"] = "Nomination and Random Map Pick restricted to these time windows.",
            ["Prop.Cooldown.Desc"] = "Number of map plays before this map can be picked again.",
            ["Prop.CooldownDateTime.Desc"] = "Real-time cooldown. Format: e.g. 2d (days) or 1m (months).",

            // ===== Mode (Current vs Legacy) =====
            ["Menu.File.Mode"] = "_Mode (schema)",
            ["Menu.File.Mode.Current"] = "Current (MapChooserSharpMS)",
            ["Menu.File.Mode.Legacy"] = "Legacy (MapChooserSharp.API v0.1.5)",
            ["Mode.Current"] = "Current",
            ["Mode.Legacy"] = "Legacy",
            ["Mode.Switched"] = "Switched mode → {0}",
            ["Mode.Banner"] = "Editing in Legacy mode (MapChooserSharp.API v0.1.5 schema). Switch via File → Mode.",
            ["Confirm.SwitchMode.Title"] = "Switch schema mode",
            ["Confirm.SwitchMode.Message"] = "Switching modes closes every loaded file. Unsaved changes will be lost:\n  • {0}\n\nProceed?",
            ["Confirm.SwitchMode.Yes"] = "Switch",
            ["Confirm.SwitchMode.No"] = "Cancel",

            // ===== Legacy-only properties =====
            ["LegacyProp.RestrictToAllowedUsersOnly"] = "Restrict to Allowed Users Only",
            ["LegacyProp.RestrictToAllowedUsersOnly.Desc"] = "Only Steam IDs in AllowedSteamIds may nominate.",
            ["LegacyProp.RequiredPermissions"] = "Required Permissions",
            ["LegacyProp.RequiredPermissions.Desc"] = "CSSharp permission flags (e.g. css/generic) required to nominate.",
            ["LegacyProp.AllowedSteamIds"] = "Allowed Steam IDs",
            ["LegacyProp.AllowedSteamIds.Desc"] = "SteamID64 list. When set, bypasses required-permission check (unless ProhibitAdminNomination + non-root).",
            ["LegacyProp.DisallowedSteamIds"] = "Disallowed Steam IDs",
            ["LegacyProp.DisallowedSteamIds.Desc"] = "SteamID64 list. Listed users cannot nominate this map.",
            ["LegacyProp.NominationCost"] = "Nomination Cost",
            ["LegacyProp.NominationCost.Desc"] = "Cost charged by NominationSystem for nominating this map.",
            ["LegacyProp.NominationSpecificCooldown"] = "Nomination Specific Cooldown",
            ["LegacyProp.NominationSpecificCooldown.Desc"] = "Per-user cooldown after nominating (plays).",
        },
        ["ja"] = new()
        {
            ["App.Title"] = "MapChooserSharpMS 設定エディタ",

            ["Menu.File"] = "ファイル(_F)",
            ["Menu.File.New"] = "新規作成(_N)",
            ["Menu.File.OpenFile"] = "ファイルを開く...(_O)",
            ["Menu.File.OpenFolder"] = "フォルダを開く...(_F)",
            ["Menu.File.Save"] = "保存(_S)",
            ["Menu.File.SaveAs"] = "名前を付けて保存...(_A)",
            ["Menu.File.SaveAll"] = "すべて保存(_L)",
            ["Menu.File.Close"] = "ファイルを閉じる(_C)",
            ["Menu.File.CloseAll"] = "すべて閉じる(_E)",
            ["Confirm.CloseAll.Title"] = "すべてのファイルを閉じる",
            ["Confirm.CloseAll.Message"] = "次のファイルには未保存の変更があり、破棄されます:\n  • {0}\n\n続行しますか?",
            ["Confirm.CloseAll.Yes"] = "すべて閉じる",
            ["Confirm.CloseAll.No"] = "キャンセル",
            ["Menu.Edit"] = "編集(_E)",
            ["Menu.Edit.Undo"] = "元に戻す(_U)",
            ["Menu.Edit.Redo"] = "やり直し(_R)",
            ["Menu.Edit.AddMap"] = "マップを追加(_M)",
            ["Menu.Edit.AddGroup"] = "グループを追加(_G)",
            ["Menu.Edit.Find"] = "検索...(_F)",
            ["Menu.Tools"] = "ツール(_T)",
            ["Menu.Tools.WorkshopCheck"] = "Workshop アイテム確認(_W)...",
            ["Menu.View"] = "表示(_V)",
            ["Menu.View.Language"] = "言語(_L)",
            ["Menu.View.DebugConsole"] = "デバッグコンソール(_D)",
            ["Debug.Header"] = "デバッグコンソール",
            ["Debug.Clear"] = "クリア",

            ["WorkshopCheck.Title"] = "Workshop アイテム状態確認",
            ["WorkshopCheck.Intro"] = "読み込み済みの全マップについて WorkshopId を Steam に問い合わせ、公開停止 / 非公開 / 削除済み のアイテムのうち、まだ Disabled になっていないものを一覧表示します。チェックで選んだものを Disabled=true に反映できます。",
            ["WorkshopCheck.Run"] = "チェック実行",
            ["WorkshopCheck.Running"] = "確認中 {0} / {1}...",
            ["WorkshopCheck.NoTargets"] = "確認対象のマップがありません (WorkshopId 未設定か既に Disabled)。",
            ["WorkshopCheck.AllPublic"] = "すべてのアイテムが公開状態です。",
            ["WorkshopCheck.FoundNonPublic"] = "非公開のアイテムを {0} 件検出しました。",
            ["WorkshopCheck.Failed"] = "チェックに失敗: {0}",
            ["WorkshopCheck.NoIssues"] = "問題はありません。チェックを実行してください。",
            ["WorkshopCheck.ApplySelected"] = "選択したマップを Disabled にする",
            ["WorkshopCheck.SelectAll"] = "すべて選択",
            ["WorkshopCheck.SelectNone"] = "選択解除",
            ["WorkshopCheck.Applied"] = "{0} 件のマップを Disabled にしました。",

            ["Banner.SectionCollisions"] = "重複セクションを検出 — TOML パーサがこの設定を拒否します。下記パスが 1 ファイルだけに残るように調整してください。",

            ["ContextMenu.RemoveFromView"] = "一覧から削除",

            ["Confirm.DiscardDirty.Title"] = "未保存の変更",
            ["Confirm.DiscardDirty.Message"] = "次のファイルには未保存の変更があり、破棄されます:\n  • {0}\n\n続行しますか?",
            ["Confirm.DiscardDirty.Yes"] = "破棄",
            ["Confirm.DiscardDirty.No"] = "キャンセル",

            ["AddEntry.Map.Title"] = "マップを追加",
            ["AddEntry.Group.Title"] = "グループを追加",
            ["AddEntry.Name"] = "名前",
            ["AddEntry.Map.NameWatermark"] = "例: de_mirage",
            ["AddEntry.Group.NameWatermark"] = "例: HardZeMaps",
            ["AddEntry.Target"] = "追加先",
            ["AddEntry.Target.Current"] = "現在のファイル ({0})",
            ["AddEntry.Target.CurrentNone"] = "現在のファイル (選択なし)",
            ["AddEntry.Target.Existing"] = "読み込み済みの別ファイル",
            ["AddEntry.Target.ExistingWatermark"] = "ファイル名で絞り込み...",
            ["AddEntry.Target.New"] = "新規ファイル (保存先を選択)",
            ["AddEntry.NewFile.PickerTitle"] = "新しい TOML ファイルの保存先を選択",

            ["Restart.Title"] = "再起動が必要です",
            ["Restart.Message"] = "言語設定を反映するため、今すぐ再起動しますか?",
            ["Restart.Yes"] = "再起動",
            ["Restart.No"] = "後で",

            ["Sidebar.Header"] = "設定ファイル",
            ["Sidebar.ToggleResolved"] = "Effective Valuesパネルの開閉",

            ["Search.Title"] = "検索",
            ["Search.Watermark"] = "名前・エイリアス・グループ名で検索",
            ["Search.NoResults"] = "一致なし",
            ["Search.Filter.Maps"] = "Map",
            ["Search.Filter.Groups"] = "Group",
            ["Search.Filter.Overrides"] = "Override",
            ["Search.Filter.Defaults"] = "Default",
            ["Search.Kind.Default"] = "Default",
            ["Search.Kind.Group"] = "Group",
            ["Search.Kind.Map"] = "Map",
            ["Search.Kind.Override"] = "Override",
            ["Search.AdvancedToggle"] = "詳細フィルタ",
            ["Search.Advanced.Intro"] = "空欄 / Any の項目はスキップされます。設定した項目と上のテキスト検索は AND で絞り込まれます。",
            ["Search.Advanced.Reset"] = "リセット",
            ["Search.Advanced.Group"] = "Group",
            ["Search.Advanced.File"] = "ファイル",
            ["Search.Advanced.Alias"] = "Alias",
            ["Search.Advanced.Desc"] = "説明",
            ["Search.Advanced.Cooldown"] = "Cooldown",
            ["Search.Advanced.Workshop"] = "Workshop ID",
            ["Search.Advanced.MaxPlayers"] = "MaxPlayers",
            ["Search.Advanced.MinPlayers"] = "MinPlayers",
            ["Search.Advanced.MapTime"] = "MapTime (分)",
            ["Search.Advanced.MapRounds"] = "MapRounds",
            ["Search.Advanced.Disabled"] = "Disabled",
            ["Search.Advanced.OnlyNom"] = "OnlyNomination",
            ["Search.Advanced.ProhibitAdmin"] = "ProhibitAdmin",
            ["Search.Advanced.Day"] = "曜日",
            ["Search.Advanced.ExtrasKey"] = "Extras キー",
            ["Search.Advanced.ExtrasValue"] = "Extras 値",
            ["Search.Help.Intro"] = "プレーンな単語は名前・エイリアスに対する部分一致、field:value 形式は項目指定の厳密な絞り込みです。すべて AND 条件。空白を含む値は \" \" で囲ってください。",
            ["Search.Help.OpsNote"] = "数値比較: = (既定), >, >=, <, <=。  真偽値: true/false/yes/no/on/off。",
            ["Search.Help.Name"] = "名前 / エイリアス / グループ名 / マップ名のいずれかに部分一致。",
            ["Search.Help.Alias"] = "MapNameAlias (表示名) のみに部分一致。",
            ["Search.Help.Desc"] = "MapDescription のみに部分一致。",
            ["Search.Help.File"] = "格納されている TOML ファイル名に部分一致。",
            ["Search.Help.Kind"] = "種別で絞り込み (上のフィルタチップと同等)。",
            ["Search.Help.Group"] = "そのグループ自身の名前、または継承先に含まれる Map / Override に一致。",
            ["Search.Help.Workshop"] = "WorkshopId が指定値と等しい。",
            ["Search.Help.Cooldown"] = "Cooldown (プレイ数) を数値比較。例: cooldown:>30",
            ["Search.Help.MaxPlayers"] = "MaxPlayers を数値比較。",
            ["Search.Help.MinPlayers"] = "MinPlayers を数値比較。",
            ["Search.Help.MapTime"] = "MapTime (分) を数値比較。",
            ["Search.Help.MapRounds"] = "MapRounds を数値比較。",
            ["Search.Help.MaxExtends"] = "MaxExtends を数値比較。",
            ["Search.Help.Disabled"] = "IsDisabled フラグ (true/false)。",
            ["Search.Help.OnlyNom"] = "OnlyNomination フラグ (true/false)。",
            ["Search.Help.Nominate"] = "OnlyNomination の反転。true = Nominate 可能。",
            ["Search.Help.ProhibitAdmin"] = "ProhibitAdminNomination フラグ (true/false)。",
            ["Search.Help.Day"] = "DaysAllowed に含まれる曜日と一致。フル名 / 前方一致どちらも可 (例: day:mon)。",
            ["Search.Help.Extra"] = "Extras エントリ: key もしくは key=value。section.key=value も指定可。",
            ["Search.Help.ExtraSection"] = "指定名を含む Extras サブセクションを持つ。",

            ["Welcome.Title"] = "MapChooserSharpMS 設定エディタ",
            ["Welcome.Description"] = "ファイル → ファイルを開く / フォルダを開く で TOML 設定ファイルを読み込みます。\n左ツリーから Default / Groups / Maps / DaySettings を選択して編集してください。",

            ["Section.Basic"] = "基本",
            ["Section.ExtendTime"] = "延長 / 時間",
            ["Section.Nomination"] = "Nominate / 選出",
            ["Section.Cooldown"] = "クールダウン",
            ["Section.Extra"] = "Extra (外部プラグイン用データ)",
            ["Section.Basic.Desc"] = "マップ/グループの基本情報と無効化。表示名・Workshop ID・無効フラグ、そしてマップの場合は継承するグループ一覧。",
            ["Section.ExtendTime.Desc"] = "マップ時間と延長挙動。MapTime / MapRounds は mp_timelimit / mp_maxrounds の初期値、Max/Time/Rounds-per-Extend は !extend 回数や 1 回あたりの延長量を制御。",
            ["Section.Nomination.Desc"] = "Nominate 可否と条件。OnlyNomination でランダム投票候補から除外、Max/MinPlayers で人数制限、ProhibitAdminNomination はコンソール専用化、DaysAllowed / AllowedTimeRanges で曜日・時間帯制限。",
            ["Section.Cooldown.Desc"] = "マップが再度選ばれるまでの待機時間。Cooldown はプレイ回数ベース、CooldownDateTime は実時間ベース (例: 2d / 1m)。",
            ["Section.Extra.Desc"] = "外部プラグイン向けのサブテーブル ([extra.セクション])。エディタはキーと値をそのまま保持し、型の解釈はプラグイン側の責任です。",

            ["Button.Reset"] = "リセット",
            ["Button.ResetAll"] = "全リセット",
            ["Button.OK"] = "OK",
            ["Button.Cancel"] = "キャンセル",
            ["Button.Add"] = "追加",
            ["Button.AddEntry"] = "+ 追加",
            ["Button.Remove"] = "削除",
            ["Button.AddSection"] = "セクション追加",
            ["Button.RemoveSection"] = "セクション削除",
            ["Button.AddKey"] = "+ キー",
            ["Button.Open"] = "開く",
            ["Button.OpenDefault"] = "Default設定を開く",
            ["Button.AddOverride"] = "+ オーバーライド追加",

            ["Status.UsingDefault"] = "デフォルトを使用中",
            ["Status.NoInheritance"] = "継承なし",
            ["Status.NoOverride"] = "上書きなし",
            ["Status.FromOverrideTarget"] = "オーバーライド元から継承",
            ["Status.Ready"] = "準備完了",

            ["Source.OverrideTargetMap"] = "オーバーライド元 (Map: {0})",
            ["Source.OverrideTargetGroup"] = "オーバーライド元 (Group: {0})",
            ["Source.Unset"] = "(未設定)",
            ["Source.Default"] = "Default",
            ["Source.DefaultFromFile"] = "Default ({0})",
            ["Source.Group"] = "Group: {0}",
            ["Source.Map"] = "Map: {0}",
            ["Source.Override"] = "Override: {0}",
            ["Source.GroupCooldownOverride"] = "Group: {0} (クールダウン上書き)",

            ["Tip.InvalidTimeRange"] = "HH:mm-HH:mm 形式で入力してください (例: 19:00-23:30)。日をまたぐ範囲も可。",
            ["Tip.Reset"] = "デフォルトに戻す (TOML からこのエントリを削除)",
            ["Tip.ResetAll"] = "全エントリを削除してデフォルトに戻す",
            ["Tip.UnknownGroup"] = "このグループ名は読み込まれたファイル群の中に存在しません。",

            ["Watermark.GroupName"] = "グループ名",
            ["Watermark.SectionName"] = "セクション名 (例: shop)",
            ["Watermark.TimeRange"] = "HH:mm-HH:mm",
            ["Watermark.ExtraKey"] = "キー",
            ["Watermark.ExtraValue"] = "値",
            ["Watermark.Filter"] = "グループ・マップを絞り込み...",
            ["Watermark.Search"] = "名前で検索...",
            ["Watermark.CooldownDateTime"] = "例: 2d または 1m",

            ["Overview.GroupsCount"] = "グループ ({0})",
            ["Overview.MapsCount"] = "マップ ({0})",
            ["Overview.NoGroups"] = "(グループなし)",
            ["Overview.NoMaps"] = "(マップなし)",
            ["Overview.DaySettingsCount"] = "DaySettings {0} 件",
            ["Overview.MapAliasFormat"] = "\u201C{0}\u201D",

            ["Category.Maps"] = "マップ一覧",
            ["Category.Groups"] = "グループ一覧",
            ["Category.EmptyMaps"] = "このファイルにはマップがまだ定義されていません。",
            ["Category.EmptyGroups"] = "このファイルにはグループがまだ定義されていません。",

            ["Label.FilePrefix"] = "ファイル: {0}",

            ["Editor.Map"] = "Map:",
            ["Editor.Group"] = "Group:",
            ["Editor.Override"] = "Override:",
            ["Editor.ParentMap"] = "Map: {0}",
            ["Editor.ParentGroup"] = "Group: {0}",

            ["DaySettings.Title"] = "DaySettings オーバーライド",
            ["DaySettings.GroupTitle"] = "DaySettings オーバーライド (このグループを参照する全マップに適用)",
            ["DaySettings.DaysLabel"] = "曜日: {0}",
            ["DaySettings.TimesLabel"] = "時間: {0}",
            ["DaySettings.PriorityLabel"] = "優先度: {0}",
            ["DaySettings.EditHint"] = "DaySettings を編集するには、左ツリーのこのマップ配下にあるオーバーライド名をクリックしてください。",

            ["Override.Trigger"] = "トリガー条件",
            ["Override.Enabled"] = "有効",
            ["Override.ForceOverride"] = "強制上書き",
            ["Override.OverridePriority"] = "優先度",
            ["Override.TargetDays"] = "対象曜日",
            ["Override.TargetTimeRanges"] = "対象時間帯",
            ["Override.SelectedDays"] = "選択中: {0}",
            ["Override.OverriddenProps"] = "上書きプロパティ (チェックが入ったフィールドのみ書き出されます)",

            ["Default.Title"] = "Default 設定",
            ["Default.Section"] = "[MapChooserSharpSettings.Default]",
            ["Default.OwnerLabel"] = "保存先ファイル: {0}",
            ["Default.RemoveOwner"] = "このファイルから切り離し",
            ["Default.RemoveOwner.Tip"] = "このファイルの Default セクションを除去します。別ファイルに再割り当てしてください。",
            ["Default.NoOwner.Title"] = "このプロジェクトにはまだ Default セクションがありません",
            ["Default.NoOwner.Body"] = "サーバは全ファイル中に [MapChooserSharpSettings.Default] が 1 つ必要です。どのファイルに持たせるかを選択してください。",
            ["Default.Assign"] = "ファイルに割り当て",
            ["Default.MultipleWarning.Title"] = "複数の Default セクションを検出",
            ["Default.MultipleWarning.Body"] = "TOML パーサは重複セクションを拒否します。下記のファイルのうち 1 つを残して他は除去するまで、サーバがこの設定をロードできません。",

            ["Resolved.Placeholder.Heading"] = "Effective Values",
            ["Resolved.Placeholder.Body"] = "マップ・グループ・オーバーライドのいずれかを選択するとプレビューされます。",
            ["Resolved.HeadingDefault"] = "Effective Values \u2014 Default",
            ["Resolved.HeadingGroup"] = "Effective Values \u2014 Group: {0}",
            ["Resolved.HeadingMap"] = "Effective Values \u2014 Map: {0}",
            ["Resolved.HeadingOverride"] = "Effective Values \u2014 Override: {0}",
            ["Resolved.ExtrasHeading"] = "Extras",
            ["Resolved.Extras.Key"] = "キー",
            ["Resolved.Extras.Value"] = "値",
            ["Resolved.Extras.Type"] = "型",
            ["Resolved.Extras.Source"] = "継承元",

            ["Status.Loaded"] = "{0} を読み込みました",
            ["Status.LoadFailed"] = "{0} の読み込みに失敗: {1}",
            ["Status.OpenedFolder"] = "フォルダ {0} を開きました",
            ["Status.NoTomlFound"] = "{0} 配下に .toml がありません",
            ["Status.Skipped"] = "{0} をスキップ: {1}",
            ["Status.Saved"] = "{0} を保存しました",
            ["Status.SaveFailed"] = "保存失敗: {0}",

            ["Extra.Prefix"] = "[extra.",

            ["Prop.MapNameAlias"] = "表示名",
            ["Prop.MapDescription"] = "マップ説明",
            ["Prop.WorkshopId"] = "Workshop ID",
            ["Prop.IsDisabled"] = "無効化",
            ["Prop.GroupSettings"] = "継承グループ",
            ["Prop.CooldownOverride"] = "クールダウン上書き (グループ専用)",
            ["Prop.MaxExtends"] = "最大延長回数",
            ["Prop.MaxExtCommandUses"] = "!ext 最大使用回数",
            ["Prop.ExtendTimePerExtends"] = "延長時間 (分)",
            ["Prop.MapTime"] = "マップ時間 (分)",
            ["Prop.ExtendRoundsPerExtends"] = "延長あたりラウンド数",
            ["Prop.MapRounds"] = "マップラウンド数",
            ["Prop.OnlyNomination"] = "Nominateのみ",
            ["Prop.MaxPlayers"] = "最大プレイヤー数",
            ["Prop.MinPlayers"] = "最小プレイヤー数",
            ["Prop.ProhibitAdminNomination"] = "管理者Nominate禁止",
            ["Prop.DaysAllowed"] = "許可曜日",
            ["Prop.AllowedTimeRanges"] = "許可時間帯",
            ["Prop.Cooldown"] = "クールダウン (プレイ回数)",
            ["Prop.CooldownDateTime"] = "クールダウン (実時間)",

            ["Prop.MapNameAlias.Desc"] = "生のマップ名の代わりに表示される名前。",
            ["Prop.MapDescription.Desc"] = "投票終了後、マップ遷移前に表示されるテキスト。",
            ["Prop.WorkshopId.Desc"] = "Workshop マップの Steam Workshop ID。",
            ["Prop.IsDisabled.Desc"] = "有効にすると、管理者でも Nominate 不可。Random Map Pick の対象からも除外されます。",
            ["Prop.GroupSettings.Desc"] = "継承元グループ一覧。衝突時は先頭のグループが優先。",
            ["Prop.CooldownOverride.Desc"] = "このグループを参照するマップの Cooldown を上書き。グループでのみ有効。",
            ["Prop.MaxExtends.Desc"] = "このマップの最大延長回数。",
            ["Prop.MaxExtCommandUses.Desc"] = "!ext コマンドの最大使用可能回数。",
            ["Prop.ExtendTimePerExtends.Desc"] = "延長1回あたり mp_timelimit に加算される分数。",
            ["Prop.MapTime.Desc"] = "mp_timelimit の初期値 (分)。",
            ["Prop.ExtendRoundsPerExtends.Desc"] = "ラウンドベース時、延長1回あたりのラウンド数。",
            ["Prop.MapRounds.Desc"] = "mp_maxrounds の初期値。",
            ["Prop.OnlyNomination.Desc"] = "Random Map Pick の対象から外し、Nominate 経由でのみ選ばれるようにします。",
            ["Prop.MaxPlayers.Desc"] = "プレイヤー数が この値以下 のときのみ Nominate / Random Map Pick 可能。",
            ["Prop.MinPlayers.Desc"] = "プレイヤー数が この値以上 のときのみ Nominate / Random Map Pick 可能。",
            ["Prop.ProhibitAdminNomination.Desc"] = "サーバコンソールからのみ Nominate 可能にする。Random Map Pick には影響しません。",
            ["Prop.DaysAllowed.Desc"] = "Nominate / Random Map Pick を許可する曜日。",
            ["Prop.AllowedTimeRanges.Desc"] = "Nominate / Random Map Pick を許可する時間帯。",
            ["Prop.Cooldown.Desc"] = "このマップが再度選ばれるまでに必要なマッププレイ回数。",
            ["Prop.CooldownDateTime.Desc"] = "実時間のクールダウン。例: 2d (日), 1m (月)。",

            ["Menu.File.Mode"] = "モード(_M) (スキーマ)",
            ["Menu.File.Mode.Current"] = "Current (MapChooserSharpMS)",
            ["Menu.File.Mode.Legacy"] = "Legacy (MapChooserSharp.API v0.1.5)",
            ["Mode.Current"] = "Current",
            ["Mode.Legacy"] = "Legacy",
            ["Mode.Switched"] = "モード切替 → {0}",
            ["Mode.Banner"] = "Legacy モードで編集中 (MapChooserSharp.API v0.1.5 スキーマ)。File → Mode で切替。",
            ["Confirm.SwitchMode.Title"] = "スキーマモード切替",
            ["Confirm.SwitchMode.Message"] = "モード切替は読み込み済みの全ファイルを閉じます。未保存の変更は失われます:\n  • {0}\n\n続行しますか?",
            ["Confirm.SwitchMode.Yes"] = "切替",
            ["Confirm.SwitchMode.No"] = "キャンセル",

            ["LegacyProp.RestrictToAllowedUsersOnly"] = "許可ユーザのみに制限",
            ["LegacyProp.RestrictToAllowedUsersOnly.Desc"] = "AllowedSteamIds に含まれるユーザのみ Nominate 可能。",
            ["LegacyProp.RequiredPermissions"] = "必要権限",
            ["LegacyProp.RequiredPermissions.Desc"] = "Nominate に必要な CSSharp 権限フラグ (例: css/generic)。",
            ["LegacyProp.AllowedSteamIds"] = "許可SteamID",
            ["LegacyProp.AllowedSteamIds.Desc"] = "SteamID64 リスト。設定時は権限チェックをバイパス (ProhibitAdminNomination + 非rootの場合を除く)。",
            ["LegacyProp.DisallowedSteamIds"] = "禁止SteamID",
            ["LegacyProp.DisallowedSteamIds.Desc"] = "SteamID64 リスト。記載されたユーザはこのマップを Nominate 不可。",
            ["LegacyProp.NominationCost"] = "Nominationコスト",
            ["LegacyProp.NominationCost.Desc"] = "NominationSystem が Nominate 時に徴収するコスト。",
            ["LegacyProp.NominationSpecificCooldown"] = "Nomination個別クールダウン",
            ["LegacyProp.NominationSpecificCooldown.Desc"] = "Nominate 後にそのユーザに掛かる個別クールダウン (プレイ回数)。",
        },
    };
}

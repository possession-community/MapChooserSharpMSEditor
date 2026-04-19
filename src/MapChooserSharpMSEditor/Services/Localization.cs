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

    static Localization()
    {
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
            ["Menu.Edit"] = "_Edit",
            ["Menu.Edit.Undo"] = "_Undo",
            ["Menu.Edit.Redo"] = "_Redo",
            ["Menu.Edit.AddMap"] = "Add _Map",
            ["Menu.Edit.AddGroup"] = "Add _Group",

            // ===== Sidebar =====
            ["Sidebar.Header"] = "Config Files",
            ["Sidebar.ToggleResolved"] = "Toggle resolved-values panel",

            // ===== Welcome =====
            ["Welcome.Title"] = "MapChooserSharpMS Config Editor",
            ["Welcome.Description"] = "Use File → Open File / Open Folder to load a TOML config.\nSelect Default / Groups / Maps / DaySettings in the left tree to edit.",

            // ===== Section headers =====
            ["Section.Basic"] = "Basic",
            ["Section.ExtendTime"] = "Extend / Time",
            ["Section.Nomination"] = "Nomination / Pick",
            ["Section.Cooldown"] = "Cooldown",
            ["Section.Extra"] = "Extra (external plugin data)",

            // ===== Common buttons / actions =====
            ["Button.Reset"] = "Reset",
            ["Button.ResetAll"] = "Reset all",
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

            // ===== Tooltips =====
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

            // ===== Resolved panel =====
            ["Resolved.Placeholder.Heading"] = "Resolved Values",
            ["Resolved.Placeholder.Body"] = "Select a map, group or override to preview.",
            ["Resolved.HeadingDefault"] = "Effective Values \u2014 Default",
            ["Resolved.HeadingGroup"] = "Effective Values \u2014 Group: {0}",
            ["Resolved.HeadingMap"] = "Effective Values \u2014 Map: {0}",
            ["Resolved.HeadingOverride"] = "Effective Values \u2014 Override: {0}",

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
            ["Prop.IsDisabled.Desc"] = "If set, the map cannot be nominated even by admin.",
            ["Prop.GroupSettings.Desc"] = "Groups whose settings this map inherits. First group wins on conflicts.",
            ["Prop.CooldownOverride.Desc"] = "Overrides Cooldown for every map that references this group. Group-only; ignored elsewhere.",
            ["Prop.MaxExtends.Desc"] = "Maximum number of extends allowed on this map.",
            ["Prop.MaxExtCommandUses.Desc"] = "Maximum times the !ext command can be invoked.",
            ["Prop.ExtendTimePerExtends.Desc"] = "Minutes added to mp_timelimit per extend.",
            ["Prop.MapTime.Desc"] = "Initial mp_timelimit value (minutes).",
            ["Prop.ExtendRoundsPerExtends.Desc"] = "Rounds added per extend (round-based cycle).",
            ["Prop.MapRounds.Desc"] = "Initial mp_maxrounds value.",
            ["Prop.OnlyNomination.Desc"] = "Exclude this map from random vote picks.",
            ["Prop.MaxPlayers.Desc"] = "Nomination allowed only when player count \u2264 this.",
            ["Prop.MinPlayers.Desc"] = "Nomination allowed only when player count \u2265 this.",
            ["Prop.ProhibitAdminNomination.Desc"] = "Only the server console can nominate.",
            ["Prop.DaysAllowed.Desc"] = "Nomination restricted to these days of week.",
            ["Prop.AllowedTimeRanges.Desc"] = "Nomination restricted to these time windows.",
            ["Prop.Cooldown.Desc"] = "Number of map plays before this map can be picked again.",
            ["Prop.CooldownDateTime.Desc"] = "Real-time cooldown. Format: e.g. 2d (days) or 1m (months).",
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
            ["Menu.Edit"] = "編集(_E)",
            ["Menu.Edit.Undo"] = "元に戻す(_U)",
            ["Menu.Edit.Redo"] = "やり直し(_R)",
            ["Menu.Edit.AddMap"] = "マップを追加(_M)",
            ["Menu.Edit.AddGroup"] = "グループを追加(_G)",

            ["Sidebar.Header"] = "設定ファイル",
            ["Sidebar.ToggleResolved"] = "Effective Valuesパネルの開閉",

            ["Welcome.Title"] = "MapChooserSharpMS 設定エディタ",
            ["Welcome.Description"] = "ファイル → ファイルを開く / フォルダを開く で TOML 設定ファイルを読み込みます。\n左ツリーから Default / Groups / Maps / DaySettings を選択して編集してください。",

            ["Section.Basic"] = "基本",
            ["Section.ExtendTime"] = "延長 / 時間",
            ["Section.Nomination"] = "推薦 / 選出",
            ["Section.Cooldown"] = "クールダウン",
            ["Section.Extra"] = "Extra (外部プラグイン用データ)",

            ["Button.Reset"] = "リセット",
            ["Button.ResetAll"] = "全リセット",
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

            ["Resolved.Placeholder.Heading"] = "Effective Values",
            ["Resolved.Placeholder.Body"] = "マップ・グループ・オーバーライドのいずれかを選択するとプレビューされます。",
            ["Resolved.HeadingDefault"] = "Effective Values \u2014 Default",
            ["Resolved.HeadingGroup"] = "Effective Values \u2014 Group: {0}",
            ["Resolved.HeadingMap"] = "Effective Values \u2014 Map: {0}",
            ["Resolved.HeadingOverride"] = "Effective Values \u2014 Override: {0}",

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
            ["Prop.ExtendTimePerExtends"] = "延長あたり時間 (分)",
            ["Prop.MapTime"] = "マップ時間 (分)",
            ["Prop.ExtendRoundsPerExtends"] = "延長あたりラウンド数",
            ["Prop.MapRounds"] = "マップラウンド数",
            ["Prop.OnlyNomination"] = "推薦のみ",
            ["Prop.MaxPlayers"] = "最大プレイヤー数",
            ["Prop.MinPlayers"] = "最小プレイヤー数",
            ["Prop.ProhibitAdminNomination"] = "管理者推薦禁止",
            ["Prop.DaysAllowed"] = "許可曜日",
            ["Prop.AllowedTimeRanges"] = "許可時間帯",
            ["Prop.Cooldown"] = "クールダウン (プレイ回数)",
            ["Prop.CooldownDateTime"] = "クールダウン (実時間)",

            ["Prop.MapNameAlias.Desc"] = "生のマップ名の代わりに表示される名前。",
            ["Prop.MapDescription.Desc"] = "投票終了後、マップ遷移前に表示されるテキスト。",
            ["Prop.WorkshopId.Desc"] = "Workshop マップの Steam Workshop ID。",
            ["Prop.IsDisabled.Desc"] = "有効にすると、管理者でも推薦不可。",
            ["Prop.GroupSettings.Desc"] = "継承元グループ一覧。衝突時は先頭のグループが優先。",
            ["Prop.CooldownOverride.Desc"] = "このグループを参照するマップの Cooldown を上書き。グループでのみ有効。",
            ["Prop.MaxExtends.Desc"] = "このマップの最大延長回数。",
            ["Prop.MaxExtCommandUses.Desc"] = "!ext コマンドの最大使用可能回数。",
            ["Prop.ExtendTimePerExtends.Desc"] = "延長1回あたり mp_timelimit に加算される分数。",
            ["Prop.MapTime.Desc"] = "mp_timelimit の初期値 (分)。",
            ["Prop.ExtendRoundsPerExtends.Desc"] = "ラウンドベース時、延長1回あたりのラウンド数。",
            ["Prop.MapRounds.Desc"] = "mp_maxrounds の初期値。",
            ["Prop.OnlyNomination.Desc"] = "ランダム投票候補から除外する。",
            ["Prop.MaxPlayers.Desc"] = "プレイヤー数が この値以下 のときのみ推薦可能。",
            ["Prop.MinPlayers.Desc"] = "プレイヤー数が この値以上 のときのみ推薦可能。",
            ["Prop.ProhibitAdminNomination.Desc"] = "サーバコンソールからのみ推薦可能にする。",
            ["Prop.DaysAllowed.Desc"] = "推薦を許可する曜日。",
            ["Prop.AllowedTimeRanges.Desc"] = "推薦を許可する時間帯。",
            ["Prop.Cooldown.Desc"] = "このマップが再度選ばれるまでに必要なマッププレイ回数。",
            ["Prop.CooldownDateTime.Desc"] = "実時間のクールダウン。例: 2d (日), 1m (月)。",
        },
    };
}

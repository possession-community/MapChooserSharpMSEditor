using System;
using System.Collections.Generic;
using System.Globalization;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// Minimal string catalog used throughout the UI. Keys are dotted namespaces
/// (e.g. <c>Prop.MaxExtends</c>, <c>Section.Basic</c>, <c>Status.UsingDefault</c>).
///
/// To add a locale, add another entry to <see cref="_strings"/>. To translate, fill in
/// keys that the default locale defines — missing keys fall back to English, which falls
/// back to the raw key so developers can spot missing entries.
///
/// Current limitation: <see cref="CurrentLocale"/> is read at startup and injected into
/// the markup extension's ProvideValue. Changing it at runtime does not re-resolve
/// already-rendered strings; a window reload is needed.
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

    private static readonly Dictionary<string, Dictionary<string, string>> _strings = new()
    {
        ["en"] = new()
        {
            // Section headers
            ["Section.Basic"] = "Basic",
            ["Section.ExtendTime"] = "Extend / Time",
            ["Section.Nomination"] = "Nomination / Pick",
            ["Section.Cooldown"] = "Cooldown",
            ["Section.Extra"] = "Extra (external plugin data)",

            // Buttons / actions
            ["Button.Reset"] = "Reset",
            ["Button.ResetAll"] = "Reset all",
            ["Button.Add"] = "Add",
            ["Button.Remove"] = "Remove",
            ["Button.AddSection"] = "Add Section",
            ["Button.RemoveSection"] = "Remove Section",
            ["Button.AddKey"] = "+ key",

            // Status / inline annotations
            ["Status.UsingDefault"] = "using default",
            ["Status.NoInheritance"] = "no inheritance",
            ["Status.NoOverride"] = "no override",

            // Tooltips
            ["Tip.Reset"] = "Revert to default (removes entry from TOML)",
            ["Tip.ResetAll"] = "Clear all entries and revert to default",

            // Property labels
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

            // Property descriptions (tooltips)
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
            ["Prop.MaxPlayers.Desc"] = "Nomination allowed only when player count ≤ this.",
            ["Prop.MinPlayers.Desc"] = "Nomination allowed only when player count ≥ this.",
            ["Prop.ProhibitAdminNomination.Desc"] = "Only the server console can nominate.",
            ["Prop.DaysAllowed.Desc"] = "Nomination restricted to these days of week.",
            ["Prop.AllowedTimeRanges.Desc"] = "Nomination restricted to these time windows.",
            ["Prop.Cooldown.Desc"] = "Number of map plays before this map can be picked again.",
            ["Prop.CooldownDateTime.Desc"] = "Real-time cooldown. Format: e.g. 2d (days) or 1m (months).",
        },
        ["ja"] = new()
        {
            ["Section.Basic"] = "基本",
            ["Section.ExtendTime"] = "延長 / 時間",
            ["Section.Nomination"] = "推薦 / 選出",
            ["Section.Cooldown"] = "クールダウン",
            ["Section.Extra"] = "Extra (外部プラグイン用データ)",

            ["Button.Reset"] = "リセット",
            ["Button.ResetAll"] = "全リセット",
            ["Button.Add"] = "追加",
            ["Button.Remove"] = "削除",
            ["Button.AddSection"] = "セクション追加",
            ["Button.RemoveSection"] = "セクション削除",
            ["Button.AddKey"] = "+ キー",

            ["Status.UsingDefault"] = "デフォルトを使用中",
            ["Status.NoInheritance"] = "継承なし",
            ["Status.NoOverride"] = "上書きなし",

            ["Tip.Reset"] = "デフォルトに戻す (TOML からこのエントリを削除)",
            ["Tip.ResetAll"] = "全エントリを削除してデフォルトに戻す",

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

namespace MapChooserSharpMSEditor.Models;

/// <summary>
/// Where a <see cref="PropertySet"/> is being edited. Drives which rows are visible in the
/// editor and which are emitted to TOML. Mirrors the distinction between
/// <c>MapGroupConfig</c> and <c>MapConfig</c> in the server.
///
/// Rules (Default is permissive — the user may want a project-wide fallback for anything):
///   * Map-only properties: <c>MapNameAlias</c>, <c>MapDescription</c>, <c>WorkshopId</c>, <c>GroupSettings</c>
///   * Group-only properties: <c>CooldownOverride</c>, <c>ShortGroupName</c>, <c>NominationLimit</c>
///   * Everything else is shared.
///   * A <c>DaySettings</c> override inherits the scope of its parent map or group.
/// </summary>
public enum PropertyScope
{
    Default,
    Group,
    Map,
}

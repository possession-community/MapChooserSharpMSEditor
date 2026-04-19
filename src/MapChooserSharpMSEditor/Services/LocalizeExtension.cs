using System;

namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// XAML markup extension: <c>{l:Localize Prop.MaxExtends}</c> resolves to the string from
/// <see cref="Localization.Get"/>. Resolved at parse time; locale changes need a re-render.
/// </summary>
public class LocalizeExtension
{
    public string Key { get; set; } = "";

    public LocalizeExtension() { }
    public LocalizeExtension(string key) { Key = key; }

    public object ProvideValue(IServiceProvider serviceProvider) => Localization.Get(Key);
}

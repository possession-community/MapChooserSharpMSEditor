namespace MapChooserSharpMSEditor.Services;

/// <summary>
/// A single entry in the Language submenu. Named class instead of a tuple so Avalonia's
/// compiled bindings can resolve <see cref="Id"/> / <see cref="NativeName"/> at XAML-compile time.
/// </summary>
public sealed record LocaleOption(string Id, string NativeName);

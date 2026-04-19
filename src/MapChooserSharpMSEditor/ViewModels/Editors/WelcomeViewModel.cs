using MapChooserSharpMSEditor.Services;

namespace MapChooserSharpMSEditor.ViewModels.Editors;

public sealed class WelcomeViewModel : ViewModelBase
{
    public string Title => Localization.Get("Welcome.Title");
    public string Description => Localization.Get("Welcome.Description");
}

namespace MapChooserSharpMSEditor.ViewModels.Editors;

public sealed class WelcomeViewModel : ViewModelBase
{
    public string Title => "MapChooserSharpMS Config Editor";
    public string Description =>
        "File → Open File / Open Folder でTOML設定ファイルを開いてください。\n" +
        "左ツリーからDefault / Groups / Maps / DaySettingsのセクションを選択して編集できます。";
}

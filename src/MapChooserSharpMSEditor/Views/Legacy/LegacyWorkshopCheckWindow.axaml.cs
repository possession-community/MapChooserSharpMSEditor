// LEGACY — remove when MCS migration completes
using Avalonia.Controls;
using Avalonia.Interactivity;
using MapChooserSharpMSEditor.ViewModels.Legacy;

namespace MapChooserSharpMSEditor.Views.Legacy;

public partial class LegacyWorkshopCheckWindow : Window
{
    public LegacyWorkshopCheckWindow() => InitializeComponent();

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LegacyWorkshopCheckViewModel vm)
        {
            var count = vm.ApplySelection();
            Tag = count;
        }
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}

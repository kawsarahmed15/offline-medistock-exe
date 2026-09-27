using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }

    private void OpenCustomizer_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.Frame.Navigate(typeof(BillCustomizerPage));
    }
}

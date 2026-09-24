using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Inventory;

public sealed partial class ExpiryDashboardPage : Page
{
    public ExpiryDashboardViewModel ViewModel { get; }

    public ExpiryDashboardPage(ExpiryDashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadDashboardAsync();
        };
    }
}

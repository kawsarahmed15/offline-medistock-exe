using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Accounting;

public sealed partial class AccountingPage : Page
{
    public AccountingViewModel ViewModel { get; }

    public AccountingPage(AccountingViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();

        Loaded += async (s, e) =>
        {
            await ViewModel.LoadInitialDataAsync();
        };
    }
}

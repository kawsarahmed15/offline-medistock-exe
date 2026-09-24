using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Compliance;

public sealed partial class ScheduleRegisterPage : Page
{
    public ScheduleRegisterViewModel ViewModel { get; }

    public ScheduleRegisterPage(ScheduleRegisterViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadRegisterAsync();
        };
    }
}

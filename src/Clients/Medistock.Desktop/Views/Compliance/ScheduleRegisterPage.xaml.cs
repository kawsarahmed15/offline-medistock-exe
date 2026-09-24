using Medistock.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Compliance;

public sealed partial class ScheduleRegisterPage : Page
{
    public ScheduleRegisterViewModel ViewModel { get; }

    public ScheduleRegisterPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ScheduleRegisterViewModel>();
        this.DataContext = ViewModel;

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadRegisterAsync();
        };
    }
}

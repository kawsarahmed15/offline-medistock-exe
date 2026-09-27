using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Compliance;

public sealed partial class GstReportsPage : Page
{
    public GstReportsViewModel ViewModel { get; }

    public GstReportsPage(GstReportsViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }
}

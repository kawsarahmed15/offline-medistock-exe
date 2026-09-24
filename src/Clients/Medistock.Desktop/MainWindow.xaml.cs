using Medistock.Desktop.Views.POS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Medistock.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        Title = "Medistock — Pharmacy ERP & POS";

        var posPage = App.Services.GetRequiredService<PosPage>();
        RootFrame.Content = posPage;
    }
}

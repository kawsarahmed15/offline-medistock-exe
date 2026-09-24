using System;
using Medistock.Desktop.Views.Accounting;
using Medistock.Desktop.Views.Compliance;
using Medistock.Desktop.Views.Inventory;
using Medistock.Desktop.Views.POS;
using Medistock.Desktop.Views.Purchases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        Title = "Medistock — Pharmacy ERP & POS";

        // Select POS as default active module
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "pos":
                    ContentFrame.Content = App.Services.GetRequiredService<PosPage>();
                    break;
                case "inventory":
                    ContentFrame.Content = App.Services.GetRequiredService<InventoryPage>();
                    break;
                case "expiry":
                    ContentFrame.Content = App.Services.GetRequiredService<ExpiryDashboardPage>();
                    break;
                case "compliance":
                    ContentFrame.Content = App.Services.GetRequiredService<ScheduleRegisterPage>();
                    break;
                case "purchases":
                    ContentFrame.Content = App.Services.GetRequiredService<PurchaseEntryPage>();
                    break;
                case "accounting":
                    ContentFrame.Content = App.Services.GetRequiredService<AccountingPage>();
                    break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;
using Medistock.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Medistock.Desktop.Views.Sales;

public sealed partial class SalesHistoryPage : Page
{
    public SalesHistoryViewModel ViewModel { get; }

    public SalesHistoryPage(SalesHistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();

        this.Loaded += async (s, e) =>
        {
            await ViewModel.LoadSalesAsync();
        };
    }

    private async void PrintInvoice_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSaleDetail != null)
        {
            await OpenPrintPreviewAsync(ViewModel.SelectedSaleDetail);
        }
    }

    private async void DownloadPdf_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSaleDetail != null)
        {
            await OpenPrintPreviewAsync(ViewModel.SelectedSaleDetail);
        }
    }

    private async Task OpenPrintPreviewAsync(Medistock.Application.Sales.DTOs.SaleDetailDto detail)
    {
        var items = detail.Items.Select(i => new ReceiptItemModel(
            i.ProductName,
            i.BatchNumber,
            i.ExpiryDate,
            i.Quantity,
            i.UnitPrice,
            i.NetAmount,
            i.TotalGstRate
        )).ToList();

        var receipt = new SaleReceiptModel(
            PharmacyName: "MEDISTOCK PHARMACY & HEALTHCARE",
            PharmacyAddress: "Main Road, Healthcare Complex, Suite 101",
            PharmacyPhone: "+91 98765 43210",
            Gstin: "19ABCDE1234F1Z5",
            DlNumbers: "20B/21B-WB-102938",
            InvoiceNo: detail.InvoiceNo,
            InvoiceDate: detail.InvoiceDate,
            CounterName: detail.CounterId ?? "Main Counter",
            CashierName: detail.UserId ?? "Pharmacist",
            CustomerName: string.IsNullOrWhiteSpace(detail.CustomerName) ? "Walk-in Customer" : detail.CustomerName,
            DoctorName: string.Empty,
            Items: items,
            Subtotal: detail.Subtotal,
            CgstAmount: detail.TaxAmount / 2,
            SgstAmount: detail.TaxAmount / 2,
            IgstAmount: 0m,
            RoundOff: detail.RoundOff,
            GrandTotal: detail.Total,
            Payments: new List<ReceiptPaymentModel>
            {
                new("Cash", detail.Total, null)
            }
        );

        try
        {
            var previewVm = App.Services.GetRequiredService<PrintPreviewViewModel>();
            await previewVm.InitializeAsync(receipt);
            var dialog = new PrintPreviewDialog(previewVm)
            {
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Preview Error: {ex.Message}";
        }
    }
}

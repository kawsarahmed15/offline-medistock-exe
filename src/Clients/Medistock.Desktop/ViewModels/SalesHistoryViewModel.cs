#pragma warning disable MVVMTK0045

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Sales.DTOs;
using Medistock.Application.Sales.Services;
using Medistock.Domain.Common;

namespace Medistock.Desktop.ViewModels;

public partial class ReturnItemRowViewModel : ObservableObject
{
    public string SaleItemId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal SoldQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CgstRate { get; set; }
    public decimal SgstRate { get; set; }
    public decimal IgstRate { get; set; }

    [ObservableProperty]
    private double _returnQuantity;

    [ObservableProperty]
    private RestockDecision _restockDecision = RestockDecision.RestockToAvailable;

    [ObservableProperty]
    private string _reason = "Customer Return";

    public double MaxReturnableQuantity => (double)SoldQuantity;

    public decimal RefundSubtotal => (decimal)ReturnQuantity * UnitPrice;
    public decimal RefundGst => RefundSubtotal * ((CgstRate + SgstRate + IgstRate) / 100m);
    public decimal RefundTotal => Math.Round(RefundSubtotal + RefundGst, 2);

    partial void OnReturnQuantityChanged(double value)
    {
        OnPropertyChanged(nameof(RefundSubtotal));
        OnPropertyChanged(nameof(RefundGst));
        OnPropertyChanged(nameof(RefundTotal));
    }
}

public partial class SalesHistoryViewModel : ObservableObject
{
    private readonly ISaleReturnService _saleReturnService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private SaleSummaryDto? _selectedSaleSummary;

    [ObservableProperty]
    private SaleDetailDto? _selectedSaleDetail;

    [ObservableProperty]
    private bool _isReturnDialogOpen;

    [ObservableProperty]
    private string _returnReason = "Customer Return";

    [ObservableProperty]
    private PaymentMode _refundMode = PaymentMode.Cash;

    [ObservableProperty]
    private decimal _totalRefundAmount;

    public ObservableCollection<SaleSummaryDto> Sales { get; } = new();
    public ObservableCollection<ReturnItemRowViewModel> ReturnRows { get; } = new();

    public SalesHistoryViewModel(ISaleReturnService saleReturnService)
    {
        _saleReturnService = saleReturnService;
        _ = LoadSalesAsync();
    }

    [RelayCommand]
    public async Task LoadSalesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading sales ledger...";

            var list = await _saleReturnService.GetSalesHistoryAsync("org-1", "branch-1", SearchQuery);

            Sales.Clear();
            foreach (var sale in list)
            {
                Sales.Add(sale);
            }

            StatusMessage = $"Loaded {Sales.Count} sales transactions.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading sales: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSaleSummaryChanged(SaleSummaryDto? value)
    {
        if (value != null)
        {
            _ = LoadSaleDetailAsync(value.Id);
        }
        else
        {
            SelectedSaleDetail = null;
        }
    }

    private async Task LoadSaleDetailAsync(string saleId)
    {
        try
        {
            var detail = await _saleReturnService.GetSaleDetailsAsync(saleId);
            SelectedSaleDetail = detail;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error fetching invoice details: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenReturnDialog()
    {
        if (SelectedSaleDetail == null)
        {
            StatusMessage = "Please select an invoice first.";
            return;
        }

        ReturnRows.Clear();
        foreach (var item in SelectedSaleDetail.Items)
        {
            var returnable = item.Quantity - item.AlreadyReturnedQuantity;
            if (returnable > 0)
            {
                var row = new ReturnItemRowViewModel
                {
                    SaleItemId = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    BatchId = item.BatchId,
                    BatchNumber = item.BatchNumber,
                    SoldQuantity = returnable,
                    UnitPrice = item.UnitPrice,
                    CgstRate = item.CgstRate,
                    SgstRate = item.SgstRate,
                    IgstRate = item.IgstRate,
                    ReturnQuantity = 0,
                    RestockDecision = RestockDecision.RestockToAvailable,
                    Reason = "Customer Return"
                };

                row.PropertyChanged += (s, e) => RecalculateRefundTotal();
                ReturnRows.Add(row);
            }
        }

        RecalculateRefundTotal();
        IsReturnDialogOpen = true;
    }

    private void RecalculateRefundTotal()
    {
        TotalRefundAmount = ReturnRows.Sum(r => r.RefundTotal);
    }

    [RelayCommand]
    public void CloseReturnDialog()
    {
        IsReturnDialogOpen = false;
    }

    [RelayCommand]
    public async Task ProcessReturnAsync()
    {
        if (SelectedSaleDetail == null) return;

        var itemsToReturn = ReturnRows.Where(r => r.ReturnQuantity > 0).ToList();
        if (!itemsToReturn.Any())
        {
            StatusMessage = "No quantities specified for return.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Posting Credit Note & Restocking Inventory...";

            var command = new ProcessSaleReturnCommand(
                OrgId: SelectedSaleDetail.OrgId,
                BranchId: SelectedSaleDetail.BranchId,
                CounterId: SelectedSaleDetail.CounterId,
                WarehouseId: SelectedSaleDetail.WarehouseId,
                OriginalSaleId: SelectedSaleDetail.Id,
                OriginalInvoiceNo: SelectedSaleDetail.InvoiceNo,
                CustomerId: SelectedSaleDetail.CustomerId,
                CustomerName: SelectedSaleDetail.CustomerName,
                UserId: "usr-admin",
                DeviceId: "dev-pos-1",
                Reason: ReturnReason,
                RefundMode: RefundMode,
                Items: itemsToReturn.Select(i => new ProcessSaleReturnItemInput(
                    SaleItemId: i.SaleItemId,
                    ProductId: i.ProductId,
                    ProductName: i.ProductName,
                    BatchId: i.BatchId,
                    BatchNumber: i.BatchNumber,
                    Quantity: (decimal)i.ReturnQuantity,
                    UnitPrice: i.UnitPrice,
                    CgstRate: i.CgstRate,
                    SgstRate: i.SgstRate,
                    IgstRate: i.IgstRate,
                    RestockDecision: i.RestockDecision,
                    Reason: i.Reason
                )).ToList()
            );

            var result = await _saleReturnService.ProcessReturnAsync(command);

            if (result.IsSuccess)
            {
                IsReturnDialogOpen = false;
                StatusMessage = $"Credit Note {result.CreditNoteNo} generated. Refund: ₹{result.TotalRefundAmount:N2}";
                await LoadSalesAsync();
                if (SelectedSaleDetail != null)
                {
                    await LoadSaleDetailAsync(SelectedSaleDetail.Id);
                }
            }
            else
            {
                StatusMessage = $"Return failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error processing return: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

#pragma warning disable MVVMTK0045

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Inventory.DTOs;
using Medistock.Application.Inventory.Services;
using Medistock.Domain.Common;

namespace Medistock.Desktop.ViewModels;

public partial class ReceiveItemRowViewModel : ObservableObject
{
    public string TransferItemId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal DispatchedQuantity { get; set; }

    [ObservableProperty]
    private double _receivedQuantity;

    public double DispatchedQuantityDouble => (double)DispatchedQuantity;
    public decimal Discrepancy => (decimal)ReceivedQuantity - DispatchedQuantity;

    partial void OnReceivedQuantityChanged(double value)
    {
        OnPropertyChanged(nameof(Discrepancy));
    }
}

public partial class StockTransfersViewModel : ObservableObject
{
    private readonly IStockTransferService _transferService;

    [ObservableProperty]
    private string _statusMessage = "Ready. Multi-branch stock transfers.";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private StockTransferSummaryDto? _selectedTransferSummary;

    [ObservableProperty]
    private StockTransferDetailDto? _selectedTransferDetail;

    [ObservableProperty]
    private bool _isCreateDialogOpen;

    [ObservableProperty]
    private bool _isReceiveDialogOpen;

    // Create Transfer Form Fields
    [ObservableProperty]
    private string _destBranchId = "branch-2";

    [ObservableProperty]
    private string _destWarehouseId = "wh-branch-2";

    [ObservableProperty]
    private string _transferNotes = "Inter-branch stock rebalance";

    public ObservableCollection<StockTransferSummaryDto> Transfers { get; } = new();
    public ObservableCollection<ReceiveItemRowViewModel> ReceiveRows { get; } = new();

    public StockTransfersViewModel(IStockTransferService transferService)
    {
        _transferService = transferService;
        _ = LoadTransfersAsync();
    }

    [RelayCommand]
    public async Task LoadTransfersAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading inter-branch transfers...";

            var list = await _transferService.GetTransfersAsync("org-1");

            Transfers.Clear();
            foreach (var t in list)
            {
                Transfers.Add(t);
            }

            StatusMessage = $"Loaded {Transfers.Count} inter-branch stock transfers.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading transfers: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedTransferSummaryChanged(StockTransferSummaryDto? value)
    {
        if (value != null)
        {
            _ = LoadTransferDetailAsync(value.Id);
        }
        else
        {
            SelectedTransferDetail = null;
        }
    }

    private async Task LoadTransferDetailAsync(string transferId)
    {
        try
        {
            var detail = await _transferService.GetTransferDetailAsync(transferId);
            SelectedTransferDetail = detail;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading transfer details: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DispatchSelectedTransferAsync()
    {
        if (SelectedTransferDetail == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Dispatching transfer {SelectedTransferDetail.TransferNo}...";

            var command = new DispatchTransferCommand(
                TransferId: SelectedTransferDetail.Id,
                DispatchedByUserId: "usr-admin",
                DeviceId: "dev-pos-1"
            );

            var result = await _transferService.DispatchTransferAsync(command);
            if (result.IsSuccess)
            {
                StatusMessage = $"Transfer {result.TransferNo} dispatched. Stock deducted from source warehouse.";
                await LoadTransfersAsync();
                if (SelectedTransferDetail != null)
                {
                    await LoadTransferDetailAsync(SelectedTransferDetail.Id);
                }
            }
            else
            {
                StatusMessage = $"Dispatch failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error dispatching transfer: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void OpenReceiveDialog()
    {
        if (SelectedTransferDetail == null) return;

        ReceiveRows.Clear();
        foreach (var item in SelectedTransferDetail.Items)
        {
            ReceiveRows.Add(new ReceiveItemRowViewModel
            {
                TransferItemId = item.Id,
                ProductName = item.ProductName,
                BatchNumber = item.BatchNumber,
                DispatchedQuantity = item.DispatchedQuantity,
                ReceivedQuantity = (double)item.DispatchedQuantity
            });
        }

        IsReceiveDialogOpen = true;
    }

    [RelayCommand]
    public void CloseReceiveDialog()
    {
        IsReceiveDialogOpen = false;
    }

    [RelayCommand]
    public async Task ProcessReceiveTransferAsync()
    {
        if (SelectedTransferDetail == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Receiving transfer {SelectedTransferDetail.TransferNo}...";

            var command = new ReceiveTransferCommand(
                TransferId: SelectedTransferDetail.Id,
                ReceivedByUserId: "usr-branch2-mgr",
                DeviceId: "dev-pos-2",
                Items: ReceiveRows.Select(r => new ReceiveTransferItemInput(
                    TransferItemId: r.TransferItemId,
                    ReceivedQuantity: (decimal)r.ReceivedQuantity
                )).ToList()
            );

            var result = await _transferService.ReceiveTransferAsync(command);
            if (result.IsSuccess)
            {
                IsReceiveDialogOpen = false;
                StatusMessage = $"Transfer {result.TransferNo} received. Inventory restored to destination branch.";
                await LoadTransfersAsync();
                if (SelectedTransferDetail != null)
                {
                    await LoadTransferDetailAsync(SelectedTransferDetail.Id);
                }
            }
            else
            {
                StatusMessage = $"Receive failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error receiving transfer: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Medistock.Infrastructure.Hardware.Export;

public record InventoryExportMetadata(
    string PharmacyName,
    string StoreAddress,
    string ContactPhone,
    string Gstin,
    DateTime ExportDate,
    int TotalProducts,
    int TotalBatches,
    int NearExpiryCount,
    int LowStockCount,
    decimal TotalStockValueCost,
    decimal TotalStockValueMrp
);

public record InventoryExportRow(
    int Index,
    string ProductName,
    string GenericName,
    string SaltComposition,
    string BatchNumber,
    DateTime ExpiryDate,
    int DaysUntilExpiry,
    string Schedule,
    decimal AvailableQuantity,
    decimal MinStockAlert,
    decimal Mrp,
    decimal PurchaseRate,
    decimal StockValueAtCost,
    decimal StockValueAtMrp,
    string StatusText,
    bool IsExpired,
    bool IsNearExpiry,
    bool IsLowStock,
    bool IsOutOfStock
);

public interface IInventoryExportService
{
    Task<string> ExportToExcelAsync(IReadOnlyList<InventoryExportRow> rows, InventoryExportMetadata meta, string? targetFilePath = null);
    Task<string> ExportToWordAsync(IReadOnlyList<InventoryExportRow> rows, InventoryExportMetadata meta, string? targetFilePath = null);
    Task<string> ExportToPdfHtmlAsync(IReadOnlyList<InventoryExportRow> rows, InventoryExportMetadata meta, string? targetFilePath = null);
}

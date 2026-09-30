using System;
using System.Collections.Generic;
using Medistock.Domain.Common;

namespace Medistock.Application.Inventory.DTOs;

public record StockSummaryItemDto(
    string ProductId,
    string ProductName,
    string? GenericName,
    string? SaltComposition,
    string? Manufacturer,
    string? CategoryName,
    DrugSchedule Schedule,
    string BatchId,
    string BatchNumber,
    DateTime ExpiryDate,
    int DaysUntilExpiry,
    ExpiryBand ExpiryStatus,
    decimal AvailableQuantity,
    decimal ReservedQuantity,
    decimal TotalQuantity,
    decimal Mrp,
    decimal PurchaseRate,
    decimal SaleRate,
    decimal StockValueAtMrp,
    decimal StockValueAtCost,
    decimal MinStockAlert = 10.0m,
    decimal GstRatePercent = 0.0m,
    decimal NetPurchaseRate = 0.0m,
    string? HsnCode = "3004"
);

public record ExpiryBandSummaryDto(
    ExpiryBand Band,
    string Title,
    int BatchCount,
    decimal TotalQuantity,
    decimal TotalValueAtRisk,
    List<StockSummaryItemDto> TopBatches
);

public record ExpiryDashboardDto(
    int TotalActiveBatches,
    decimal TotalStockQuantity,
    decimal TotalInventoryValueCost,
    decimal TotalInventoryValueMrp,
    ExpiryBandSummaryDto ExpiredBand,
    ExpiryBandSummaryDto CriticalBand,
    ExpiryBandSummaryDto WarningBand,
    ExpiryBandSummaryDto GoodBand
);

public record StockAdjustmentRequest(
    string OrgId,
    string BranchId,
    string WarehouseId,
    string ProductId,
    string BatchId,
    StockAdjustmentType AdjustmentType,
    decimal Quantity,
    string Reason,
    string UserId,
    string DeviceId
);

public record StockAdjustmentResult(
    bool Success,
    string? MovementId,
    decimal NewAvailableQuantity,
    string? ErrorMessage = null
);

public record ScheduleDrugRegisterDto(
    string Id,
    string InvoiceNo,
    DateTime SaleDate,
    string ProductName,
    DrugSchedule Schedule,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal Quantity,
    string PatientName,
    string? PatientPhone,
    string? PatientAddress,
    string DoctorName,
    string? DoctorRegNo,
    string? PrescriptionRef,
    DateTime? PrescriptionDate,
    string DispensedByUserId
);

public record ScheduleDrugFilter(
    DateTime? StartDate,
    DateTime? EndDate,
    DrugSchedule? Schedule,
    string? SearchQuery,
    int Limit = 100
);

public record UpdateProductDetailsCommand(
    string ProductId,
    string ProductName,
    string? GenericName,
    string? SaltComposition,
    string? Manufacturer,
    string? CategoryName,
    string? HsnCode,
    decimal GstRatePercent,
    DrugSchedule Schedule,
    decimal MinStockAlert,
    string? BatchId,
    string? BatchNumber,
    DateTime? ExpiryDate,
    decimal? Mrp,
    decimal? PurchaseRate,
    decimal? SaleRate
);

public record UpdateProductDetailsResult(
    bool Success,
    string? ErrorMessage = null
);

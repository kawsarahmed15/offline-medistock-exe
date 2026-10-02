using System;
using Medistock.Domain.Common;

namespace Medistock.Application.Products.Queries;

public class ProductSearchDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string GenericName { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public DosageForm DosageForm { get; set; }
    public string PackSizeDescription { get; set; } = string.Empty;
    public string HsnCode { get; set; } = string.Empty;
    public decimal GstRatePercent { get; set; }
    public DrugSchedule Schedule { get; set; }
    public bool IsPrescriptionRequired { get; set; }
    public bool IsColdChain { get; set; }
    public bool IsNarcotic { get; set; }
    public string? ManufacturerName { get; set; }
    public string? Barcode { get; set; }
    public string? BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? NearestExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal SaleRate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal MinStockAlert { get; set; } = 10;
    public List<ProductBatchDto> Batches { get; set; } = new();
}

public class ProductBatchDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal PurchaseRate { get; set; }
    public decimal SaleRate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal MinStockAlert { get; set; } = 10;
    public int NearExpiryDays { get; set; } = 90;

    public bool IsExpired => ExpiryDate.Date <= DateTime.UtcNow.Date;
    public bool IsNearExpiry => !IsExpired && ExpiryDate.Date <= DateTime.UtcNow.AddDays(NearExpiryDays).Date;
    public bool IsOutOfStock => AvailableQuantity <= 0;
    public bool IsLowStock => !IsOutOfStock && AvailableQuantity <= MinStockAlert;

    public string StatusText => IsExpired 
        ? "EXPIRED" 
        : (IsNearExpiry 
            ? "EXP NEAR" 
            : (IsOutOfStock 
                ? "OUT OF STOCK" 
                : (IsLowStock ? "LOW STOCK" : "AVAILABLE")));

    public string StockDisplay => IsOutOfStock 
        ? "0 (OOS)" 
        : (IsLowStock ? $"{AvailableQuantity:0.#} (LOW)" : $"{AvailableQuantity:0.#}");

    public string StockForegroundHex => IsOutOfStock 
        ? "#DC2626" 
        : (IsLowStock ? "#D97706" : "#16A34A");

    public string ExpiryForegroundHex => (IsExpired || IsNearExpiry) 
        ? "#DC2626" 
        : "#64748B";

    public string RowBackgroundHex => IsExpired 
        ? "#35DC2626" 
        : (IsNearExpiry ? "#22DC2626" : "#00000000");

    public string RowBorderHex => IsExpired 
        ? "#DC2626" 
        : (IsNearExpiry ? "#80DC2626" : "#00000000");

    public string StatusForegroundHex => (IsExpired || IsOutOfStock) 
        ? "#DC2626" 
        : ((IsNearExpiry || IsLowStock) ? "#D97706" : "#16A34A");

    public string StatusBackgroundHex => (IsExpired || IsNearExpiry || IsOutOfStock) 
        ? "#25DC2626" 
        : (IsLowStock ? "#25D97706" : "#2016A34A");
}

public class BarcodeLookupDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string GenericName { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal SaleRate { get; set; }
    public decimal GstRatePercent { get; set; }
    public DrugSchedule Schedule { get; set; }
    public bool IsColdChain { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal MinStockAlert { get; set; } = 10;
    public string Barcode { get; set; } = string.Empty;
    public string PackSizeDescription { get; set; } = "1x10";
    public DosageForm DosageForm { get; set; } = DosageForm.Tablet;
}

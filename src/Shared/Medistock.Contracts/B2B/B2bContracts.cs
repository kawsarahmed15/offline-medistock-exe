using System;
using System.Collections.Generic;

namespace Medistock.Contracts.B2B;

public record WholesalerCatalogItemDto
{
    public required string CatalogId { get; init; }
    public required string WholesalerId { get; init; }
    public required string WholesalerName { get; init; }
    public required string ProductCode { get; init; }
    public required string BrandName { get; init; }
    public required string GenericName { get; init; }
    public required string DosageForm { get; init; }
    public required string Strength { get; init; }
    public required string Manufacturer { get; init; }
    public required string HsnCode { get; init; }
    public decimal Mrp { get; init; }
    public decimal WholesaleRate { get; init; }
    public decimal GstRate { get; init; }
    public int AvailableStock { get; init; }
    public string? SchemeDescription { get; init; } // e.g. "10 + 1 Free" or "5% Extra Margin"
    public int MinimumOrderQuantity { get; init; } = 1;
    public int FreeRatioBuy { get; init; } // Buy X
    public int FreeRatioGet { get; init; } // Get Y
}

public record CreateB2bOrderRequest
{
    public required string WholesalerId { get; init; }
    public required string DeliveryAddress { get; init; }
    public string? Notes { get; init; }
    public required List<CreateB2bOrderItemDto> Items { get; init; } = new();
}

public record CreateB2bOrderItemDto
{
    public required string CatalogId { get; init; }
    public required string ProductCode { get; init; }
    public required string ProductName { get; init; }
    public int OrderQuantity { get; init; }
    public decimal UnitWholesaleRate { get; init; }
    public decimal GstRate { get; init; }
    public int FreeQuantity { get; init; }
}

public record B2bOrderSummaryDto
{
    public required string OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required string WholesalerId { get; init; }
    public required string WholesalerName { get; init; }
    public required string Status { get; init; } // Draft, Submitted, Confirmed, Dispatched, Delivered, Cancelled
    public decimal SubTotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public int TotalItems { get; init; }
    public DateTime OrderDateUtc { get; init; }
    public DateTime? ExpectedDeliveryUtc { get; init; }
    public string? DispatchTrackingNumber { get; init; }
}

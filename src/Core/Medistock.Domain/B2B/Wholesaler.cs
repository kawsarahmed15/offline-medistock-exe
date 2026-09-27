using System;
using System.Collections.Generic;
using Medistock.Domain.Common;

namespace Medistock.Domain.B2B;

public enum B2bOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Confirmed = 3,
    Dispatched = 4,
    Delivered = 5,
    Cancelled = 6
}

public class Wholesaler : Entity<string>
{
    public string Name { get; private set; } = string.Empty;
    public string Gstin { get; private set; } = string.Empty;
    public string DrugLicenseNo { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public decimal MinOrderValue { get; private set; }
    public int CreditDays { get; private set; } = 30;
    public bool IsVerified { get; private set; } = true;
    public double Rating { get; private set; } = 4.8;

    private Wholesaler() { }

    public static Wholesaler Create(
        string id,
        string name,
        string gstin,
        string drugLicenseNo,
        string phone,
        string email,
        string city,
        string state,
        decimal minOrderValue = 0,
        int creditDays = 30,
        double rating = 4.8)
    {
        return new Wholesaler
        {
            Id = id,
            Name = name,
            Gstin = gstin,
            DrugLicenseNo = drugLicenseNo,
            Phone = phone,
            Email = email,
            City = city,
            State = state,
            MinOrderValue = minOrderValue,
            CreditDays = creditDays,
            IsVerified = true,
            Rating = rating
        };
    }
}

public class B2bOrderItem : Entity<string>
{
    public string OrderId { get; private set; } = string.Empty;
    public string CatalogId { get; private set; } = string.Empty;
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int OrderQuantity { get; private set; }
    public int FreeQuantity { get; private set; }
    public decimal UnitWholesaleRate { get; private set; }
    public decimal GstRate { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    private B2bOrderItem() { }

    public static B2bOrderItem Create(
        string id,
        string orderId,
        string catalogId,
        string productCode,
        string productName,
        int orderQuantity,
        int freeQuantity,
        decimal unitWholesaleRate,
        decimal gstRate)
    {
        var taxable = orderQuantity * unitWholesaleRate;
        var tax = Math.Round(taxable * (gstRate / 100m), 2, MidpointRounding.AwayFromZero);
        var total = taxable + tax;

        return new B2bOrderItem
        {
            Id = id,
            OrderId = orderId,
            CatalogId = catalogId,
            ProductCode = productCode,
            ProductName = productName,
            OrderQuantity = orderQuantity,
            FreeQuantity = freeQuantity,
            UnitWholesaleRate = unitWholesaleRate,
            GstRate = gstRate,
            TaxAmount = tax,
            TotalAmount = total
        };
    }
}

public class B2bOrder : AggregateRoot<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string BranchId { get; private set; } = string.Empty;
    public string OrderNumber { get; private set; } = string.Empty;
    public string WholesalerId { get; private set; } = string.Empty;
    public B2bOrderStatus Status { get; private set; } = B2bOrderStatus.Draft;
    public decimal SubTotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string DeliveryAddress { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string? DispatchTrackingNumber { get; private set; }
    public DateTime OrderedAt { get; private set; }
    public DateTime? ExpectedDelivery { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public string? ConvertedPurchaseInvoiceId { get; private set; }

    private readonly List<B2bOrderItem> _items = new();
    public IReadOnlyList<B2bOrderItem> Items => _items.AsReadOnly();

    private B2bOrder() { }

    public static B2bOrder Create(
        string id,
        string orgId,
        string branchId,
        string orderNumber,
        string wholesalerId,
        string deliveryAddress,
        string? notes = null)
    {
        return new B2bOrder
        {
            Id = id,
            OrgId = orgId,
            BranchId = branchId,
            OrderNumber = orderNumber,
            WholesalerId = wholesalerId,
            Status = B2bOrderStatus.Draft,
            DeliveryAddress = deliveryAddress,
            Notes = notes,
            OrderedAt = DateTime.UtcNow
        };
    }

    public void AddItem(B2bOrderItem item)
    {
        _items.Add(item);
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        decimal subtotal = 0;
        decimal tax = 0;
        foreach (var item in _items)
        {
            subtotal += item.OrderQuantity * item.UnitWholesaleRate;
            tax += item.TaxAmount;
        }

        SubTotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        TaxAmount = Math.Round(tax, 2, MidpointRounding.AwayFromZero);
        TotalAmount = Math.Round(SubTotal + TaxAmount, 2, MidpointRounding.AwayFromZero);
    }

    public void SubmitOrder()
    {
        if (Status != B2bOrderStatus.Draft)
            throw new InvalidOperationException($"Cannot submit order in status {Status}");

        Status = B2bOrderStatus.Submitted;
        OrderedAt = DateTime.UtcNow;
    }

    public void ConfirmOrder(DateTime expectedDelivery)
    {
        Status = B2bOrderStatus.Confirmed;
        ExpectedDelivery = expectedDelivery;
    }

    public void MarkDispatched(string trackingNumber)
    {
        Status = B2bOrderStatus.Dispatched;
        DispatchTrackingNumber = trackingNumber;
    }

    public void MarkDelivered(string? purchaseInvoiceId = null)
    {
        Status = B2bOrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        ConvertedPurchaseInvoiceId = purchaseInvoiceId;
    }

    public void CancelOrder(string reason)
    {
        if (Status == B2bOrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel an already delivered order");

        Status = B2bOrderStatus.Cancelled;
        Notes = (Notes != null ? Notes + " | Cancelled: " : "Cancelled: ") + reason;
    }
}

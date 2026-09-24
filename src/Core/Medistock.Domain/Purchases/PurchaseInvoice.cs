using System;
using System.Collections.Generic;
using System.Linq;
using Medistock.Domain.Common;

namespace Medistock.Domain.Purchases;

public enum PurchaseInvoiceStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2
}

public class PurchaseInvoiceItem : Entity<string>
{
    public string PurchaseInvoiceId { get; private set; } = string.Empty;
    public string ProductId { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string HsnCode { get; private set; } = string.Empty;
    public string BatchNumber { get; private set; } = string.Empty;
    public DateTime ExpiryDate { get; private set; }
    public DateTime? ManufacturingDate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal FreeQuantity { get; private set; }
    public decimal TotalQuantity => Quantity + FreeQuantity;
    public decimal UnitPrice { get; private set; } // Purchase rate
    public decimal Mrp { get; private set; }
    public decimal SaleRate { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal GstRatePercent { get; private set; }
    public decimal CgstRate { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstRate { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal LandedCostPerUnit => TotalQuantity > 0 ? Math.Round(NetAmount / TotalQuantity, 4) : 0;

    private PurchaseInvoiceItem() { }

    public static PurchaseInvoiceItem Create(
        string id,
        string purchaseInvoiceId,
        string productId,
        string productName,
        string hsnCode,
        string batchNumber,
        DateTime expiryDate,
        decimal quantity,
        decimal freeQuantity,
        decimal unitPrice,
        decimal mrp,
        decimal saleRate,
        decimal discountPct,
        decimal gstRatePercent,
        bool isInterstate,
        DateTime? manufacturingDate = null)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("Batch number is mandatory.", nameof(batchNumber));

        var gross = quantity * unitPrice;
        var discountAmt = Math.Round(gross * (discountPct / 100m), 2);
        var taxable = gross - discountAmt;

        decimal cgstRate = 0, cgstAmt = 0;
        decimal sgstRate = 0, sgstAmt = 0;
        decimal igstRate = 0, igstAmt = 0;

        if (isInterstate)
        {
            igstRate = gstRatePercent;
            igstAmt = Math.Round(taxable * (igstRate / 100m), 2);
        }
        else
        {
            cgstRate = gstRatePercent / 2m;
            cgstAmt = Math.Round(taxable * (cgstRate / 100m), 2);
            sgstRate = gstRatePercent / 2m;
            sgstAmt = Math.Round(taxable * (sgstRate / 100m), 2);
        }

        var net = taxable + cgstAmt + sgstAmt + igstAmt;

        return new PurchaseInvoiceItem
        {
            Id = id,
            PurchaseInvoiceId = purchaseInvoiceId,
            ProductId = productId,
            ProductName = productName,
            HsnCode = hsnCode,
            BatchNumber = batchNumber.Trim().ToUpperInvariant(),
            ExpiryDate = expiryDate,
            ManufacturingDate = manufacturingDate,
            Quantity = quantity,
            FreeQuantity = freeQuantity,
            UnitPrice = unitPrice,
            Mrp = mrp,
            SaleRate = saleRate > 0 ? saleRate : mrp,
            DiscountPct = discountPct,
            DiscountAmount = discountAmt,
            TaxableAmount = taxable,
            GstRatePercent = gstRatePercent,
            CgstRate = cgstRate,
            CgstAmount = cgstAmt,
            SgstRate = sgstRate,
            SgstAmount = sgstAmt,
            IgstRate = igstRate,
            IgstAmount = igstAmt,
            NetAmount = net
        };
    }
}

public class PurchaseInvoice : AggregateRoot<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string BranchId { get; private set; } = string.Empty;
    public string WarehouseId { get; private set; } = string.Empty;
    public string SupplierId { get; private set; } = string.Empty;
    public string SupplierName { get; private set; } = string.Empty;
    public string? SupplierGstin { get; private set; }
    public string SupplierInvoiceNo { get; private set; } = string.Empty;
    public DateTime SupplierInvoiceDate { get; private set; }
    public PurchaseInvoiceStatus Status { get; private set; } = PurchaseInvoiceStatus.Draft;
    public bool IsInterstate { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal RoundOff { get; private set; }
    public decimal GrandTotal { get; private set; }
    public string? Notes { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; private set; }

    private readonly List<PurchaseInvoiceItem> _items = new();
    public IReadOnlyList<PurchaseInvoiceItem> Items => _items.AsReadOnly();

    private PurchaseInvoice() { }

    public static PurchaseInvoice Create(
        string id,
        string orgId,
        string branchId,
        string warehouseId,
        string supplierId,
        string supplierName,
        string? supplierGstin,
        string supplierInvoiceNo,
        DateTime supplierInvoiceDate,
        bool isInterstate,
        string createdByUserId,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(supplierInvoiceNo))
            throw new ArgumentException("Supplier invoice number is mandatory.", nameof(supplierInvoiceNo));

        return new PurchaseInvoice
        {
            Id = id,
            OrgId = orgId,
            BranchId = branchId,
            WarehouseId = warehouseId,
            SupplierId = supplierId,
            SupplierName = supplierName,
            SupplierGstin = supplierGstin,
            SupplierInvoiceNo = supplierInvoiceNo.Trim(),
            SupplierInvoiceDate = supplierInvoiceDate,
            IsInterstate = isInterstate,
            Status = PurchaseInvoiceStatus.Draft,
            CreatedByUserId = createdByUserId,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(PurchaseInvoiceItem item)
    {
        if (Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Cannot modify items of a posted purchase invoice.");

        _items.Add(item);
        RecalculateTotals();
    }

    public void MarkPosted()
    {
        if (Status == PurchaseInvoiceStatus.Posted)
            throw new InvalidOperationException("Invoice is already posted.");
        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot post a purchase invoice with no line items.");

        Status = PurchaseInvoiceStatus.Posted;
        PostedAt = DateTime.UtcNow;
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.Quantity * i.UnitPrice);
        DiscountAmount = _items.Sum(i => i.DiscountAmount);
        TaxableAmount = _items.Sum(i => i.TaxableAmount);
        CgstAmount = _items.Sum(i => i.CgstAmount);
        SgstAmount = _items.Sum(i => i.SgstAmount);
        IgstAmount = _items.Sum(i => i.IgstAmount);

        var rawTotal = TaxableAmount + CgstAmount + SgstAmount + IgstAmount;
        var rounded = Math.Round(rawTotal, MidpointRounding.AwayFromZero);
        RoundOff = rounded - rawTotal;
        GrandTotal = rounded;
    }
}

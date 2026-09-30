using System;
using System.Collections.Generic;
using System.Linq;
using Medistock.Domain.Common;

namespace Medistock.Domain.Sales;

public class Sale : AggregateRoot<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string BranchId { get; private set; } = string.Empty;
    public string CounterId { get; private set; } = string.Empty;
    public string WarehouseId { get; private set; } = string.Empty;
    public string? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public string? PatientId { get; private set; }
    public string? DoctorId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string DeviceId { get; private set; } = string.Empty;
    public string InvoiceNo { get; private set; } = string.Empty;
    public DateTime InvoiceDate { get; private set; } = DateTime.UtcNow;
    public SaleStatus Status { get; private set; } = SaleStatus.Draft;
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal RoundOff { get; private set; }
    public decimal Total { get; private set; }
    public bool IsInterstate { get; private set; }
    public string? PrescriptionRef { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; private set; }

    private readonly List<SaleItem> _items = new();
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

    private readonly List<SalePayment> _payments = new();
    public IReadOnlyCollection<SalePayment> Payments => _payments.AsReadOnly();

    private Sale() { }

    public static Sale Create(
        string id,
        string orgId,
        string branchId,
        string counterId,
        string warehouseId,
        string userId,
        string deviceId,
        string invoiceNo,
        string? customerId = null,
        string? customerName = null,
        bool isInterstate = false,
        string? prescriptionRef = null)
    {
        return new Sale
        {
            Id = id,
            OrgId = orgId,
            BranchId = branchId,
            CounterId = counterId,
            WarehouseId = warehouseId,
            UserId = userId,
            DeviceId = deviceId,
            InvoiceNo = invoiceNo,
            CustomerId = customerId,
            CustomerName = customerName,
            IsInterstate = isInterstate,
            PrescriptionRef = prescriptionRef,
            Status = SaleStatus.Draft,
            InvoiceDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(SaleItem item)
    {
        if (Status == SaleStatus.Posted)
            throw new InvalidOperationException("Cannot modify items on a posted invoice.");

        _items.Add(item);
        RecalculateTotals();
    }

    public void RemoveItem(string itemId)
    {
        if (Status == SaleStatus.Posted)
            throw new InvalidOperationException("Cannot modify items on a posted invoice.");

        _items.RemoveAll(i => i.Id == itemId);
        RecalculateTotals();
    }

    public void AddPayment(SalePayment payment)
    {
        _payments.Add(payment);
    }

    public void PostSale()
    {
        if (Status == SaleStatus.Posted) return;
        if (!_items.Any())
            throw new InvalidOperationException("Cannot post a sale with no line items.");

        var totalPaid = _payments.Sum(p => p.Amount);
        if (totalPaid < (Total - 0.05m) && string.IsNullOrWhiteSpace(CustomerId))
        {
            throw new InvalidOperationException("Walk-in customer must pay full bill total.");
        }

        Status = SaleStatus.Posted;
        PostedAt = DateTime.UtcNow;
    }

    public void HoldBill()
    {
        if (Status != SaleStatus.Draft) return;
        Status = SaleStatus.Held;
    }

    public void ResumeBill()
    {
        if (Status != SaleStatus.Held) return;
        Status = SaleStatus.Draft;
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.TaxableAmount);
        DiscountAmount = _items.Sum(i => i.DiscountAmount);
        TaxAmount = _items.Sum(i => i.TotalGst);

        var rawTotal = Subtotal + TaxAmount;
        var roundedTotal = Math.Round(rawTotal, 0, MidpointRounding.AwayFromZero);
        RoundOff = roundedTotal - rawTotal;
        Total = roundedTotal;
    }
}

public class SaleItem : Entity<string>
{
    public string SaleId { get; private set; } = string.Empty;
    public string ProductId { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string BatchId { get; private set; } = string.Empty;
    public string BatchNumber { get; private set; } = string.Empty;
    public DateTime ExpiryDate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Mrp { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CgstRate { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstRate { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal TotalGst => CgstAmount + SgstAmount + IgstAmount;
    public decimal NetAmount { get; private set; }

    private SaleItem() { }

    public static SaleItem Create(
        string id,
        string saleId,
        string productId,
        string productName,
        string batchId,
        string batchNumber,
        DateTime expiryDate,
        decimal quantity,
        decimal unitPrice,
        decimal mrp,
        decimal gstRatePercent,
        bool isInterstate,
        decimal discountPct = 0)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

        var grossAmount = quantity * unitPrice;
        var discountAmt = Math.Round(grossAmount * (discountPct / 100m), 2, MidpointRounding.AwayFromZero);
        var net = grossAmount - discountAmt;

        var taxable = gstRatePercent > 0
            ? Math.Round((net * 100m) / (100m + gstRatePercent), 2, MidpointRounding.AwayFromZero)
            : net;

        var totalGst = net - taxable;

        decimal cgstRate = 0, cgstAmt = 0, sgstRate = 0, sgstAmt = 0, igstRate = 0, igstAmt = 0;

        if (isInterstate)
        {
            igstRate = gstRatePercent;
            igstAmt = totalGst;
        }
        else
        {
            cgstRate = gstRatePercent / 2m;
            sgstRate = gstRatePercent / 2m;
            cgstAmt = Math.Round(totalGst / 2m, 2, MidpointRounding.AwayFromZero);
            sgstAmt = totalGst - cgstAmt;
        }

        return new SaleItem
        {
            Id = id,
            SaleId = saleId,
            ProductId = productId,
            ProductName = productName,
            BatchId = batchId,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Mrp = mrp,
            DiscountPct = discountPct,
            DiscountAmount = discountAmt,
            TaxableAmount = taxable,
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

public class SalePayment : Entity<string>
{
    public string SaleId { get; private set; } = string.Empty;
    public PaymentMode PaymentMode { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public DateTime PaidAt { get; private set; } = DateTime.UtcNow;

    private SalePayment() { }

    public static SalePayment Create(string id, string saleId, PaymentMode mode, decimal amount, string? reference = null)
    {
        return new SalePayment
        {
            Id = id,
            SaleId = saleId,
            PaymentMode = mode,
            Amount = amount,
            Reference = reference,
            PaidAt = DateTime.UtcNow
        };
    }
}

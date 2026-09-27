using System;
using System.Collections.Generic;
using System.Linq;
using Medistock.Domain.Common;

namespace Medistock.Domain.Sales;

public class SaleReturn : AggregateRoot<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string BranchId { get; private set; } = string.Empty;
    public string CounterId { get; private set; } = string.Empty;
    public string WarehouseId { get; private set; } = string.Empty;
    public string OriginalSaleId { get; private set; } = string.Empty;
    public string OriginalInvoiceNo { get; private set; } = string.Empty;
    public string CreditNoteNo { get; private set; } = string.Empty;
    public DateTime ReturnDate { get; private set; } = DateTime.UtcNow;
    public string? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string DeviceId { get; private set; } = string.Empty;
    public SaleReturnStatus Status { get; private set; } = SaleReturnStatus.Draft;
    public string Reason { get; private set; } = string.Empty;
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal RoundOff { get; private set; }
    public decimal TotalAmount { get; private set; }
    public PaymentMode RefundMode { get; private set; } = PaymentMode.Cash;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; private set; }

    private readonly List<SaleReturnItem> _items = new();
    public IReadOnlyCollection<SaleReturnItem> Items => _items.AsReadOnly();

    private SaleReturn() { }

    public static SaleReturn Create(
        string id,
        string orgId,
        string branchId,
        string counterId,
        string warehouseId,
        string originalSaleId,
        string originalInvoiceNo,
        string creditNoteNo,
        string? customerId,
        string? customerName,
        string userId,
        string deviceId,
        string reason,
        PaymentMode refundMode = PaymentMode.Cash)
    {
        return new SaleReturn
        {
            Id = id,
            OrgId = orgId,
            BranchId = branchId,
            CounterId = counterId,
            WarehouseId = warehouseId,
            OriginalSaleId = originalSaleId,
            OriginalInvoiceNo = originalInvoiceNo,
            CreditNoteNo = creditNoteNo,
            CustomerId = customerId,
            CustomerName = customerName,
            UserId = userId,
            DeviceId = deviceId,
            Reason = reason,
            RefundMode = refundMode,
            Status = SaleReturnStatus.Draft,
            ReturnDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddItem(SaleReturnItem item)
    {
        if (Status == SaleReturnStatus.Posted)
            throw new InvalidOperationException("Cannot modify items on a posted credit note.");

        _items.Add(item);
        RecalculateTotals();
    }

    public void PostReturn()
    {
        if (Status == SaleReturnStatus.Posted)
            throw new InvalidOperationException("Sale return is already posted.");

        if (!_items.Any())
            throw new InvalidOperationException("Cannot post a return with zero items.");

        RecalculateTotals();
        Status = SaleReturnStatus.Posted;
        PostedAt = DateTime.UtcNow;
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.TaxableAmount);
        TaxAmount = _items.Sum(i => i.CgstAmount + i.SgstAmount + i.IgstAmount);
        var rawTotal = Subtotal + TaxAmount;
        var rounded = Math.Round(rawTotal, 0, MidpointRounding.AwayFromZero);
        RoundOff = rounded - rawTotal;
        TotalAmount = rounded;
    }
}

public class SaleReturnItem : Entity<string>
{
    public string SaleReturnId { get; private set; } = string.Empty;
    public string SaleItemId { get; private set; } = string.Empty;
    public string ProductId { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string BatchId { get; private set; } = string.Empty;
    public string BatchNumber { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CgstRate { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstRate { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public RestockDecision RestockDecision { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    private SaleReturnItem() { }

    public static SaleReturnItem Create(
        string id,
        string saleReturnId,
        string saleItemId,
        string productId,
        string productName,
        string batchId,
        string batchNumber,
        decimal quantity,
        decimal unitPrice,
        decimal cgstRate,
        decimal sgstRate,
        decimal igstRate,
        RestockDecision restockDecision,
        string reason)
    {
        if (quantity <= 0)
            throw new ArgumentException("Return quantity must be greater than zero.", nameof(quantity));

        var taxable = quantity * unitPrice;
        var cgst = taxable * (cgstRate / 100m);
        var sgst = taxable * (sgstRate / 100m);
        var igst = taxable * (igstRate / 100m);
        var net = taxable + cgst + sgst + igst;

        return new SaleReturnItem
        {
            Id = id,
            SaleReturnId = saleReturnId,
            SaleItemId = saleItemId,
            ProductId = productId,
            ProductName = productName,
            BatchId = batchId,
            BatchNumber = batchNumber,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxableAmount = Math.Round(taxable, 2, MidpointRounding.AwayFromZero),
            CgstRate = cgstRate,
            CgstAmount = Math.Round(cgst, 2, MidpointRounding.AwayFromZero),
            SgstRate = sgstRate,
            SgstAmount = Math.Round(sgst, 2, MidpointRounding.AwayFromZero),
            IgstRate = igstRate,
            IgstAmount = Math.Round(igst, 2, MidpointRounding.AwayFromZero),
            NetAmount = Math.Round(net, 2, MidpointRounding.AwayFromZero),
            RestockDecision = restockDecision,
            Reason = reason
        };
    }
}

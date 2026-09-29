using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Purchases.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Purchases;

namespace Medistock.Application.Purchases.Services;

public interface IPurchaseService
{
    Task<PurchasePostingResult> CreateAndPostPurchaseInvoiceAsync(
        CreatePurchaseInvoiceCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(string orgId, CancellationToken cancellationToken = default);
    Task<string> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceSummaryDto>> GetRecentPurchasesAsync(string orgId, string branchId, int limit = 100, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDetailsDto?> GetPurchaseInvoiceDetailsAsync(string invoiceId, CancellationToken cancellationToken = default);
    Task<PurchaseKpiSummaryDto> GetPurchaseKpiSummaryAsync(string orgId, string branchId, string period = "All", CancellationToken cancellationToken = default);
    Task<bool> IsDuplicateInvoiceAsync(string orgId, string supplierId, string supplierInvoiceNo, CancellationToken cancellationToken = default);
    Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(string invoiceId, string cancelledByUserId, CancellationToken cancellationToken = default);
}

public class PurchaseService : IPurchaseService
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISupplierRepository _supplierRepository;

    public PurchaseService(
        IPurchaseRepository purchaseRepository,
        ISupplierRepository supplierRepository)
    {
        _purchaseRepository = purchaseRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<PurchasePostingResult> CreateAndPostPurchaseInvoiceAsync(
        CreatePurchaseInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Items == null || command.Items.Count == 0)
        {
            return new PurchasePostingResult(false, null, 0, 0, 0, "Purchase invoice must have at least one line item.");
        }

        if (string.IsNullOrWhiteSpace(command.SupplierInvoiceNo))
        {
            return new PurchasePostingResult(false, null, 0, 0, 0, "Supplier invoice number is mandatory.");
        }

        var invoiceId = Guid.NewGuid().ToString("N");
        var invoice = PurchaseInvoice.Create(
            id: invoiceId,
            orgId: command.OrgId,
            branchId: command.BranchId,
            warehouseId: command.WarehouseId,
            supplierId: command.SupplierId,
            supplierName: command.SupplierName,
            supplierGstin: command.SupplierGstin,
            supplierInvoiceNo: command.SupplierInvoiceNo,
            supplierInvoiceDate: command.SupplierInvoiceDate,
            isInterstate: command.IsInterstate,
            createdByUserId: command.CreatedByUserId,
            notes: command.Notes
        );

        var batches = new List<Batch>();
        var movements = new List<StockMovement>();

        foreach (var itemInput in command.Items)
        {
            var itemId = Guid.NewGuid().ToString("N");
            var item = PurchaseInvoiceItem.Create(
                id: itemId,
                purchaseInvoiceId: invoiceId,
                productId: itemInput.ProductId,
                productName: itemInput.ProductName,
                hsnCode: itemInput.HsnCode,
                batchNumber: itemInput.BatchNumber,
                expiryDate: itemInput.ExpiryDate,
                quantity: itemInput.Quantity,
                freeQuantity: itemInput.FreeQuantity,
                unitPrice: itemInput.UnitPrice,
                mrp: itemInput.Mrp,
                saleRate: itemInput.SaleRate,
                discountPct: itemInput.DiscountPct,
                gstRatePercent: itemInput.GstRatePercent,
                isInterstate: command.IsInterstate,
                manufacturingDate: itemInput.ManufacturingDate
            );

            invoice.AddItem(item);

            // Create/prepare batch
            var batchId = Guid.NewGuid().ToString("N");
            var batch = Batch.Create(
                id: batchId,
                productId: itemInput.ProductId,
                orgId: command.OrgId,
                batchNumber: itemInput.BatchNumber,
                expiryDate: itemInput.ExpiryDate,
                mrp: itemInput.Mrp,
                purchaseRate: itemInput.UnitPrice,
                saleRate: itemInput.SaleRate,
                manufacturingDate: itemInput.ManufacturingDate
            );
            batches.Add(batch);

            // Create inward stock movement
            var totalQty = itemInput.Quantity + itemInput.FreeQuantity;
            var movement = StockMovement.Record(
                id: Guid.NewGuid().ToString("N"),
                orgId: command.OrgId,
                branchId: command.BranchId,
                warehouseId: command.WarehouseId,
                batchId: batchId,
                productId: itemInput.ProductId,
                movementType: StockMovementType.Purchase,
                quantity: totalQty,
                referenceType: "PURCHASE_INVOICE",
                referenceId: command.SupplierInvoiceNo,
                unitCost: item.LandedCostPerUnit,
                userId: command.CreatedByUserId,
                deviceId: "DESKTOP-01"
            );
            movements.Add(movement);
        }

        invoice.MarkPosted();

        // Build Outbox event
        var outboxPayload = JsonSerializer.Serialize(new
        {
            InvoiceId = invoice.Id,
            SupplierId = invoice.SupplierId,
            SupplierInvoiceNo = invoice.SupplierInvoiceNo,
            GrandTotal = invoice.GrandTotal,
            ItemCount = invoice.Items.Count,
            PostedAt = invoice.PostedAt
        });

        var outboxEvent = OutboxEvent.Create(
            id: Guid.NewGuid().ToString("N"),
            aggregateType: "PurchaseInvoice",
            aggregateId: invoice.Id,
            eventType: "PurchaseInvoicePosted",
            payloadJson: outboxPayload,
            deviceId: "DESKTOP-01",
            operationId: Guid.NewGuid().ToString("N")
        );

        return await _purchaseRepository.PostPurchaseInvoiceAtomicAsync(
            invoice, batches, movements, outboxEvent, cancellationToken);
    }

    public Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(string orgId, CancellationToken cancellationToken = default)
    {
        return _supplierRepository.GetAllSuppliersAsync(orgId, cancellationToken);
    }

    public Task<string> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken cancellationToken = default)
    {
        var supplier = Supplier.Create(
            id: Guid.NewGuid().ToString("N"),
            orgId: command.OrgId,
            name: command.Name,
            gstin: command.Gstin,
            dlNumber: command.DlNumber,
            phone: command.Phone,
            email: command.Email,
            address: command.Address,
            creditDays: command.CreditDays,
            openingBalance: command.OpeningBalance
        );

        return _supplierRepository.CreateSupplierAsync(supplier, cancellationToken);
    }

    public Task<IReadOnlyList<PurchaseInvoiceSummaryDto>> GetRecentPurchasesAsync(string orgId, string branchId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return _purchaseRepository.GetPurchaseInvoicesAsync(orgId, branchId, limit, cancellationToken);
    }

    public Task<PurchaseInvoiceDetailsDto?> GetPurchaseInvoiceDetailsAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        return _purchaseRepository.GetPurchaseInvoiceDetailsAsync(invoiceId, cancellationToken);
    }

    public Task<PurchaseKpiSummaryDto> GetPurchaseKpiSummaryAsync(string orgId, string branchId, string period = "All", CancellationToken cancellationToken = default)
    {
        return _purchaseRepository.GetPurchaseKpiSummaryAsync(orgId, branchId, period, cancellationToken);
    }

    public Task<bool> IsDuplicateInvoiceAsync(string orgId, string supplierId, string supplierInvoiceNo, CancellationToken cancellationToken = default)
    {
        return _purchaseRepository.IsDuplicateInvoiceAsync(orgId, supplierId, supplierInvoiceNo, cancellationToken);
    }

    public Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(string invoiceId, string cancelledByUserId, CancellationToken cancellationToken = default)
    {
        return _purchaseRepository.CancelPurchaseInvoiceAsync(invoiceId, cancelledByUserId, cancellationToken);
    }
}

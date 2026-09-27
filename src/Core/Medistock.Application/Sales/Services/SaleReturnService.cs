using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Sales.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Sales;

namespace Medistock.Application.Sales.Services;

public interface ISaleReturnService
{
    Task<IReadOnlyList<SaleSummaryDto>> GetSalesHistoryAsync(
        string orgId,
        string branchId,
        string? searchQuery = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<SaleDetailDto?> GetSaleDetailsAsync(
        string saleId,
        CancellationToken cancellationToken = default);

    Task<SaleReturnResult> ProcessReturnAsync(
        ProcessSaleReturnCommand command,
        CancellationToken cancellationToken = default);
}

public class SaleReturnService : ISaleReturnService
{
    private readonly ISaleReturnRepository _saleReturnRepository;
    private readonly IDocumentSequenceService _sequenceService;

    public SaleReturnService(
        ISaleReturnRepository saleReturnRepository,
        IDocumentSequenceService sequenceService)
    {
        _saleReturnRepository = saleReturnRepository;
        _sequenceService = sequenceService;
    }

    public async Task<IReadOnlyList<SaleSummaryDto>> GetSalesHistoryAsync(
        string orgId,
        string branchId,
        string? searchQuery = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _saleReturnRepository.GetRecentSalesAsync(orgId, branchId, searchQuery, limit, cancellationToken);
    }

    public async Task<SaleDetailDto?> GetSaleDetailsAsync(
        string saleId,
        CancellationToken cancellationToken = default)
    {
        return await _saleReturnRepository.GetSaleByIdAsync(saleId, cancellationToken);
    }

    public async Task<SaleReturnResult> ProcessReturnAsync(
        ProcessSaleReturnCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Items == null || command.Items.Count == 0)
        {
            return new SaleReturnResult(false, null, null, 0, "No items selected for return.");
        }

        var returnId = $"ret_{Ulid.NewUlid()}";
        var creditNoteNo = await _sequenceService.GenerateInvoiceNumberAsync(command.OrgId, command.BranchId, "CN", cancellationToken);

        var saleReturn = SaleReturn.Create(
            returnId,
            command.OrgId,
            command.BranchId,
            command.CounterId,
            command.WarehouseId,
            command.OriginalSaleId,
            command.OriginalInvoiceNo,
            creditNoteNo,
            command.CustomerId,
            command.CustomerName,
            command.UserId,
            command.DeviceId,
            command.Reason,
            command.RefundMode);

        var stockMovements = new List<StockMovement>();

        foreach (var item in command.Items)
        {
            var returnItemId = $"ri_{Ulid.NewUlid()}";
            var returnItem = SaleReturnItem.Create(
                returnItemId,
                returnId,
                item.SaleItemId,
                item.ProductId,
                item.ProductName,
                item.BatchId,
                item.BatchNumber,
                item.Quantity,
                item.UnitPrice,
                item.CgstRate,
                item.SgstRate,
                item.IgstRate,
                item.RestockDecision,
                item.Reason);

            saleReturn.AddItem(returnItem);

            // If decision is to RestockToAvailable, increment active stock balance
            // If Quarantine, record stock movement under damage/quarantine
            var movementType = item.RestockDecision switch
            {
                RestockDecision.RestockToAvailable => StockMovementType.SaleReturn,
                RestockDecision.QuarantineDamaged => StockMovementType.Damage,
                RestockDecision.QuarantineExpired => StockMovementType.ExpiryWriteOff,
                _ => StockMovementType.SaleReturn
            };

            var movementId = $"sm_{Ulid.NewUlid()}";
            var movement = StockMovement.Record(
                movementId,
                command.OrgId,
                command.BranchId,
                command.WarehouseId,
                item.BatchId,
                item.ProductId,
                movementType,
                item.Quantity, // Positive quantity restores/moves inventory
                "SALE_RETURN",
                creditNoteNo,
                item.UnitPrice,
                command.UserId,
                command.DeviceId);

            stockMovements.Add(movement);
        }

        saleReturn.PostReturn();

        var outboxPayload = JsonSerializer.Serialize(new
        {
            SaleReturnId = saleReturn.Id,
            CreditNoteNo = saleReturn.CreditNoteNo,
            OriginalSaleId = saleReturn.OriginalSaleId,
            OriginalInvoiceNo = saleReturn.OriginalInvoiceNo,
            OrgId = saleReturn.OrgId,
            BranchId = saleReturn.BranchId,
            TotalAmount = saleReturn.TotalAmount,
            ItemsCount = saleReturn.Items.Count,
            Timestamp = DateTime.UtcNow
        });

        var outboxEvent = OutboxEvent.Create(
            $"evt_{Ulid.NewUlid()}",
            "SaleReturn",
            saleReturn.Id,
            "SALE_RETURNED",
            outboxPayload,
            command.DeviceId,
            $"op_{Ulid.NewUlid()}");

        return await _saleReturnRepository.CommitSaleReturnAtomicAsync(
            saleReturn,
            stockMovements,
            outboxEvent,
            cancellationToken);
    }
}

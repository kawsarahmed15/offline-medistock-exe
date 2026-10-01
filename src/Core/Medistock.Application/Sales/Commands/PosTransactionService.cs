using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Sales;

namespace Medistock.Application.Sales.Commands;

public interface IPosTransactionService
{
    Task<CommitSaleResult> ProcessSaleAsync(CommitSaleCommand command, CancellationToken cancellationToken = default);
}

public class PosTransactionService : IPosTransactionService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IDocumentSequenceService _sequenceService;

    public PosTransactionService(
        ISaleRepository saleRepository,
        IDocumentSequenceService sequenceService)
    {
        _saleRepository = saleRepository;
        _sequenceService = sequenceService;
    }

    public async Task<CommitSaleResult> ProcessSaleAsync(CommitSaleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Items == null || command.Items.Count == 0)
        {
            return new CommitSaleResult(false, null, null, 0, 0, DateTime.UtcNow, "Cannot commit an empty sale.");
        }

        var saleId = Ulid.NewUlid().ToString();
        var invoiceNo = await _sequenceService.GenerateInvoiceNumberAsync(command.OrgId, command.BranchId, "INV", cancellationToken);

        var sale = Sale.Create(
            saleId,
            command.OrgId,
            command.BranchId,
            command.CounterId,
            command.WarehouseId,
            command.UserId,
            command.DeviceId,
            invoiceNo,
            command.CustomerId,
            command.CustomerName,
            command.IsInterstate,
            command.PrescriptionRef);

        var stockMovements = new List<StockMovement>();

        foreach (var item in command.Items)
        {
            var unitPrice = (item.Mrp > 0 && item.UnitPrice > item.Mrp) ? item.Mrp : item.UnitPrice;
            var itemId = Ulid.NewUlid().ToString();
            var saleItem = SaleItem.Create(
                itemId,
                saleId,
                item.ProductId,
                item.ProductName,
                item.BatchId,
                item.BatchNumber,
                item.ExpiryDate,
                item.Quantity,
                unitPrice,
                item.Mrp,
                item.GstRatePercent,
                command.IsInterstate,
                item.DiscountPercent);

            sale.AddItem(saleItem);

            var movementId = Ulid.NewUlid().ToString();
            var movement = StockMovement.Record(
                movementId,
                command.OrgId,
                command.BranchId,
                command.WarehouseId,
                item.BatchId,
                item.ProductId,
                StockMovementType.Sale,
                -item.Quantity,
                "SALE",
                invoiceNo,
                saleItem.UnitPrice,
                command.UserId,
                command.DeviceId);

            stockMovements.Add(movement);
        }

        if (command.Payments != null)
        {
            foreach (var payment in command.Payments)
            {
                var paymentId = Ulid.NewUlid().ToString();
                var paymentAmount = (command.Payments.Count == 1 && payment.PaymentMode != PaymentMode.Credit)
                    ? sale.Total
                    : payment.Amount;
                sale.AddPayment(SalePayment.Create(paymentId, saleId, payment.PaymentMode, paymentAmount, payment.Reference));
            }
        }

        sale.PostSale();

        var outboxPayload = JsonSerializer.Serialize(new
        {
            SaleId = sale.Id,
            InvoiceNo = sale.InvoiceNo,
            OrgId = sale.OrgId,
            BranchId = sale.BranchId,
            CounterId = sale.CounterId,
            WarehouseId = sale.WarehouseId,
            UserId = sale.UserId,
            DeviceId = sale.DeviceId,
            CustomerId = sale.CustomerId,
            CustomerName = sale.CustomerName,
            Subtotal = sale.Subtotal,
            TaxAmount = sale.TaxAmount,
            RoundOff = sale.RoundOff,
            Total = sale.Total,
            ItemsCount = sale.Items.Count,
            Timestamp = DateTime.UtcNow
        });

        var outboxEvent = OutboxEvent.Create(
            Ulid.NewUlid().ToString(),
            "Sale",
            sale.Id,
            "SALE_COMMITTED",
            outboxPayload,
            command.DeviceId,
            Ulid.NewUlid().ToString());

        return await _saleRepository.CommitSaleAtomicAsync(sale, stockMovements, outboxEvent, cancellationToken);
    }
}

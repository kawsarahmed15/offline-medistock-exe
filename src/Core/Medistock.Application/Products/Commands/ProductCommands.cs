using System;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Common;
using Medistock.Domain.Inventory;
using Medistock.Domain.Products;

namespace Medistock.Application.Products.Commands;

public record CreateProductWithBatchCommand(
    string OrgId,
    string WarehouseId,
    string Name,
    string? BrandName,
    string? GenericName,
    string? Composition,
    string? Strength,
    DosageForm DosageForm,
    int PackUnits,
    string BaseUnit,
    string HsnCode,
    decimal GstRatePercent,
    DrugSchedule Schedule,
    string? PrimaryBarcode,
    string? ManufacturerName,
    bool IsColdChain,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal Mrp,
    decimal PurchaseRate,
    decimal SaleRate,
    decimal OpeningQuantity
);

public record CreateProductResult(
    bool Success,
    string? ProductId,
    string? BatchId,
    string? ErrorMessage
);

public interface IProductService
{
    Task<CreateProductResult> CreateProductWithBatchAsync(
        CreateProductWithBatchCommand command,
        CancellationToken cancellationToken = default);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<CreateProductResult> CreateProductWithBatchAsync(
        CreateProductWithBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new CreateProductResult(false, null, null, "Product name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(command.BatchNumber))
        {
            return new CreateProductResult(false, null, null, "Batch number cannot be empty.");
        }

        if (command.Mrp <= 0)
        {
            return new CreateProductResult(false, null, null, "MRP must be greater than zero.");
        }

        try
        {
            var productId = Ulid.NewUlid().ToString();
            var batchId = Ulid.NewUlid().ToString();

            var packSize = new PackSize(command.PackUnits > 0 ? command.PackUnits : 10, string.IsNullOrWhiteSpace(command.BaseUnit) ? "TAB" : command.BaseUnit.Trim().ToUpperInvariant());

            var product = Product.Create(
                productId,
                command.OrgId,
                command.Name,
                command.BrandName ?? command.Name,
                command.GenericName ?? string.Empty,
                command.Composition ?? string.Empty,
                command.Strength ?? string.Empty,
                command.DosageForm,
                packSize,
                command.HsnCode ?? "3004",
                command.GstRatePercent,
                command.Schedule,
                command.PrimaryBarcode,
                manufacturerName: command.ManufacturerName,
                isColdChain: command.IsColdChain
            );

            var batch = Batch.Create(
                batchId,
                productId,
                command.OrgId,
                command.BatchNumber,
                command.ExpiryDate,
                command.Mrp,
                command.PurchaseRate,
                command.SaleRate > 0 ? command.SaleRate : command.Mrp
            );

            await _productRepository.CreateProductWithBatchAsync(
                product,
                batch,
                command.OpeningQuantity,
                command.WarehouseId,
                cancellationToken
            );

            return new CreateProductResult(true, productId, batchId, null);
        }
        catch (Exception ex)
        {
            return new CreateProductResult(false, null, null, ex.Message);
        }
    }
}

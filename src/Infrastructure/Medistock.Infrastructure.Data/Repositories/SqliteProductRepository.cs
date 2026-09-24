using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Domain.Inventory;
using Medistock.Domain.Products;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteProductRepository : IProductRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteProductRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string> CreateProductWithBatchAsync(
        Product product,
        Batch batch,
        decimal openingQuantity,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            const string insertProductSql = @"
                INSERT INTO products (
                    id, org_id, name, brand_name, generic_name, composition, strength,
                    dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
                    schedule, is_prescription_required, is_cold_chain, is_narcotic,
                    is_active, primary_barcode, manufacturer_name, created_at
                ) VALUES (
                    @Id, @OrgId, @Name, @BrandName, @GenericName, @Composition, @Strength,
                    @DosageForm, @PackUnits, @BaseUnit, @HsnCode, @GstRatePercent,
                    @Schedule, @IsPrescriptionRequired, @IsColdChain, @IsNarcotic,
                    1, @PrimaryBarcode, @ManufacturerName, @CreatedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertProductSql,
                new
                {
                    product.Id,
                    product.OrgId,
                    product.Name,
                    product.BrandName,
                    product.GenericName,
                    product.Composition,
                    product.Strength,
                    DosageForm = (int)product.DosageForm,
                    PackUnits = product.PackSize.UnitsPerPack,
                    BaseUnit = product.PackSize.BaseUnit,
                    product.HsnCode,
                    product.GstRatePercent,
                    Schedule = (int)product.Schedule,
                    IsPrescriptionRequired = product.IsPrescriptionRequired ? 1 : 0,
                    IsColdChain = product.IsColdChain ? 1 : 0,
                    IsNarcotic = product.IsNarcotic ? 1 : 0,
                    product.PrimaryBarcode,
                    product.ManufacturerName,
                    CreatedAt = product.CreatedAt.ToString("o")
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            ));

            if (!string.IsNullOrWhiteSpace(product.PrimaryBarcode))
            {
                const string insertBarcodeSql = @"
                    INSERT OR IGNORE INTO product_barcodes (id, product_id, barcode, created_at)
                    VALUES (@Id, @ProductId, @Barcode, @CreatedAt);
                ";
                await connection.ExecuteAsync(new CommandDefinition(
                    insertBarcodeSql,
                    new
                    {
                        Id = Ulid.NewUlid().ToString(),
                        ProductId = product.Id,
                        Barcode = product.PrimaryBarcode.Trim(),
                        CreatedAt = DateTime.UtcNow.ToString("o")
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                ));
            }

            const string insertBatchSql = @"
                INSERT INTO batches (
                    id, product_id, org_id, batch_number, expiry_date,
                    mrp, purchase_rate, sale_rate, created_at
                ) VALUES (
                    @Id, @ProductId, @OrgId, @BatchNumber, @ExpiryDate,
                    @Mrp, @PurchaseRate, @SaleRate, @CreatedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertBatchSql,
                new
                {
                    batch.Id,
                    batch.ProductId,
                    batch.OrgId,
                    batch.BatchNumber,
                    ExpiryDate = batch.ExpiryDate.ToString("o"),
                    batch.Mrp,
                    batch.PurchaseRate,
                    batch.SaleRate,
                    CreatedAt = batch.CreatedAt.ToString("o")
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            ));

            var stockBalanceId = Ulid.NewUlid().ToString();
            const string insertStockSql = @"
                INSERT INTO stock_balances (
                    id, batch_id, product_id, warehouse_id, quantity, reserved_quantity, last_updated_at
                ) VALUES (
                    @Id, @BatchId, @ProductId, @WarehouseId, @Quantity, 0.0, @LastUpdatedAt
                );
            ";

            await connection.ExecuteAsync(new CommandDefinition(
                insertStockSql,
                new
                {
                    Id = stockBalanceId,
                    BatchId = batch.Id,
                    ProductId = product.Id,
                    WarehouseId = string.IsNullOrWhiteSpace(warehouseId) ? "wh-1" : warehouseId,
                    Quantity = openingQuantity,
                    LastUpdatedAt = DateTime.UtcNow.ToString("o")
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            ));

            transaction.Commit();
            return product.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}

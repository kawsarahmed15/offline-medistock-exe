using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.B2B.Services;
using Medistock.Application.Purchases.Services;
using Medistock.Contracts.B2B;
using Medistock.Domain.B2B;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class B2bCommerceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteB2bCommerceRepository _b2bRepository;
    private readonly SqlitePurchaseRepository _purchaseRepository;
    private readonly SqliteSupplierRepository _supplierRepository;
    private readonly PurchaseService _purchaseService;
    private readonly B2bCommerceService _b2bService;

    public B2bCommerceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_b2b_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _migrator.MigrateAsync().GetAwaiter().GetResult();

        _b2bRepository = new SqliteB2bCommerceRepository(_connectionFactory);
        _supplierRepository = new SqliteSupplierRepository(_connectionFactory);
        var outboxRepo = new SqliteOutboxRepository(_connectionFactory);
        _purchaseRepository = new SqlitePurchaseRepository(_connectionFactory, outboxRepo, _supplierRepository);
        _purchaseService = new PurchaseService(_purchaseRepository, _supplierRepository);
        _b2bService = new B2bCommerceService(_b2bRepository, _purchaseService, _supplierRepository);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task GetWholesalersAndCatalog_ShouldReturnSeededData()
    {
        // Act
        var wholesalers = await _b2bService.GetWholesalersAsync();
        var catalog = await _b2bService.SearchCatalogAsync();

        // Assert
        Assert.NotEmpty(wholesalers);
        Assert.Contains(wholesalers, w => w.Name.Contains("Apex Pharma"));
        Assert.NotEmpty(catalog);
        Assert.Contains(catalog, c => c.BrandName.Contains("Dolo"));
    }

    [Fact]
    public async Task CreateAndSubmitOrder_ShouldCalculateTaxAndCreateOutbox()
    {
        // Arrange
        var catalog = await _b2bService.SearchCatalogAsync();
        var dolo = catalog.First(c => c.BrandName.Contains("Dolo"));

        var req = new CreateB2bOrderRequest
        {
            WholesalerId = dolo.WholesalerId,
            DeliveryAddress = "Medistock Central Store, Main Street",
            Notes = "Urgent replenishment",
            Items = new List<CreateB2bOrderItemDto>
            {
                new()
                {
                    CatalogId = dolo.CatalogId,
                    ProductCode = dolo.ProductCode,
                    ProductName = dolo.BrandName,
                    OrderQuantity = 50,
                    FreeQuantity = 5,
                    UnitWholesaleRate = dolo.WholesaleRate,
                    GstRate = dolo.GstRate
                }
            }
        };

        // Act
        var summary = await _b2bService.CreateAndSubmitOrderAsync(
            req, "ORG-001", "BR-MAIN", "USR-01", "POS-01");

        // Assert
        Assert.NotNull(summary.OrderId);
        Assert.StartsWith("PO-B2B-", summary.OrderNumber);
        Assert.Equal("Submitted", summary.Status);
        Assert.Equal(50 * dolo.WholesaleRate, summary.SubTotal);
        Assert.True(summary.TaxAmount > 0);
        Assert.Equal(summary.SubTotal + summary.TaxAmount, summary.TotalAmount);

        // Verify in DB
        var order = await _b2bService.GetOrderByIdAsync(summary.OrderId);
        Assert.NotNull(order);
        Assert.Equal(B2bOrderStatus.Submitted, order.Status);
        Assert.Single(order.Items);
        Assert.Equal(5, order.Items[0].FreeQuantity);
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldTransitionStateMachine()
    {
        // Arrange
        var catalog = await _b2bService.SearchCatalogAsync();
        var item = catalog.First();

        var req = new CreateB2bOrderRequest
        {
            WholesalerId = item.WholesalerId,
            DeliveryAddress = "Store 1",
            Items = new List<CreateB2bOrderItemDto>
            {
                new()
                {
                    CatalogId = item.CatalogId,
                    ProductCode = item.ProductCode,
                    ProductName = item.BrandName,
                    OrderQuantity = 10,
                    UnitWholesaleRate = item.WholesaleRate,
                    GstRate = item.GstRate
                }
            }
        };

        var summary = await _b2bService.CreateAndSubmitOrderAsync(req, "ORG-001", "BR-MAIN", "USR-01", "POS-01");

        // Act & Assert 1: Confirmed
        await _b2bService.UpdateOrderStatusAsync(summary.OrderId, B2bOrderStatus.Confirmed);
        var order = await _b2bService.GetOrderByIdAsync(summary.OrderId);
        Assert.Equal(B2bOrderStatus.Confirmed, order!.Status);

        // Act & Assert 2: Dispatched
        await _b2bService.UpdateOrderStatusAsync(summary.OrderId, B2bOrderStatus.Dispatched, "TRACK-DELHIVERY-9988");
        order = await _b2bService.GetOrderByIdAsync(summary.OrderId);
        Assert.Equal(B2bOrderStatus.Dispatched, order!.Status);
        Assert.Equal("TRACK-DELHIVERY-9988", order.DispatchTrackingNumber);
    }

    [Fact]
    public async Task ReceiveOrderAndConvertToPurchaseInvoice_ShouldCreateInvoiceAndRestock()
    {
        // Arrange: Seed product in products table
        var seeder = new DataSeeder(_connectionFactory);
        await seeder.SeedIfEmptyAsync();
        await seeder.SeedSampleProductsAsync();

        var catalog = await _b2bService.SearchCatalogAsync();
        var dolo = catalog.First(c => c.BrandName.Contains("Dolo"));

        var req = new CreateB2bOrderRequest
        {
            WholesalerId = dolo.WholesalerId,
            DeliveryAddress = "Medistock Main Store",
            Items = new List<CreateB2bOrderItemDto>
            {
                new()
                {
                    CatalogId = dolo.CatalogId,
                    ProductCode = "p_dolo", // Match seeded product ID
                    ProductName = "Dolo 650mg Tablet",
                    OrderQuantity = 100,
                    FreeQuantity = 10,
                    UnitWholesaleRate = 24.50m,
                    GstRate = 12.0m
                }
            }
        };

        var summary = await _b2bService.CreateAndSubmitOrderAsync(req, "org-1", "br-1", "usr-1", "pos-1");

        // Act
        var result = await _b2bService.ReceiveOrderAndConvertToPurchaseInvoiceAsync(
            summary.OrderId, "wh-1", "usr-1", "pos-1");

        // Assert
        Assert.True(result.Success, result.ErrorMessage ?? "Unknown failure");
        Assert.NotNull(result.PurchaseInvoiceId);
        Assert.True(result.GrandTotal > 0);

        // Verify order status is delivered and linked to purchase invoice
        var order = await _b2bService.GetOrderByIdAsync(summary.OrderId);
        Assert.NotNull(order);
        Assert.Equal(B2bOrderStatus.Delivered, order.Status);
        Assert.NotNull(order.DeliveredAt);
        Assert.Equal(result.PurchaseInvoiceId, order.ConvertedPurchaseInvoiceId);
    }
}

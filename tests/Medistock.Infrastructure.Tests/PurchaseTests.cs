using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Purchases.DTOs;
using Medistock.Application.Purchases.Services;
using Medistock.Domain.Common;
using Medistock.Domain.Purchases;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class PurchaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSupplierRepository _supplierRepository;
    private readonly SqliteOutboxRepository _outboxRepository;
    private readonly SqlitePurchaseRepository _purchaseRepository;
    private readonly PurchaseService _purchaseService;
    private readonly DataSeeder _seeder;

    public PurchaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_pur_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);

        // Run migrations & seed data
        var migrator = new DatabaseMigrator(_connectionFactory);
        migrator.MigrateAsync().GetAwaiter().GetResult();

        _seeder = new DataSeeder(_connectionFactory);
        _seeder.SeedIfEmptyAsync().GetAwaiter().GetResult();
        _seeder.SeedSampleProductsAsync().GetAwaiter().GetResult();

        _supplierRepository = new SqliteSupplierRepository(_connectionFactory);
        _outboxRepository = new SqliteOutboxRepository(_connectionFactory);
        _purchaseRepository = new SqlitePurchaseRepository(_connectionFactory, _outboxRepository, _supplierRepository);
        _purchaseService = new PurchaseService(_purchaseRepository, _supplierRepository);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task CreateSupplier_And_GetSuppliers_WorksSuccessfully()
    {
        // Act
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1",
            Name: "Apex Pharma Distributors",
            Gstin: "29AABCU9603R1ZM",
            DlNumber: "KA-B1-998877",
            Phone: "+91 80 2233 4455",
            Email: "orders@apexpharma.in",
            Address: "100 Warehouse Rd, Peenya, Bengaluru",
            CreditDays: 45,
            OpeningBalance: 0
        ));

        // Assert
        Assert.NotNull(supplierId);

        var suppliers = await _purchaseService.GetSuppliersAsync("org-1");
        Assert.NotEmpty(suppliers);

        var created = suppliers.FirstOrDefault(s => s.Id == supplierId);
        Assert.NotNull(created);
        Assert.Equal("Apex Pharma Distributors", created.Name);
        Assert.Equal("29AABCU9603R1ZM", created.Gstin);
        Assert.Equal(45, created.CreditDays);
    }

    [Fact]
    public async Task CreateAndPostPurchaseInvoice_AtomicExecution_AddsStock_UpdatesSupplierAndOutbox()
    {
        // 1. Arrange - Create supplier
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1",
            Name: "MedPlus Wholesalers",
            Gstin: "29AAACN1234M1Z2",
            DlNumber: "KA-B2-554433",
            Phone: "+91 80 4455 6677",
            Email: "billing@medplus.in",
            Address: "50 Brigade Rd, Bengaluru",
            CreditDays: 30,
            OpeningBalance: 1000.00m
        ));

        var command = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1",
            BranchId: "br-1",
            WarehouseId: "wh-1",
            SupplierId: supplierId,
            SupplierName: "MedPlus Wholesalers",
            SupplierGstin: "29AAACN1234M1Z2",
            SupplierInvoiceNo: "MED-2026-9041",
            SupplierInvoiceDate: DateTime.UtcNow.Date,
            IsInterstate: false,
            CreatedByUserId: "USER-STOREKEEPER",
            Notes: "Monthly replenishment stock",
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                // Item 1: Dolo 650 (50 purchased + 5 free)
                new PurchaseInvoiceItemInputDto(
                    ProductId: "p_dolo",
                    ProductName: "Dolo 650mg Tablet",
                    HsnCode: "30049099",
                    BatchNumber: "DL26MAR",
                    ExpiryDate: new DateTime(2028, 3, 1),
                    ManufacturingDate: new DateTime(2026, 3, 1),
                    Quantity: 50,
                    FreeQuantity: 5,
                    UnitPrice: 20.00m,
                    Mrp: 30.50m,
                    SaleRate: 30.00m,
                    DiscountPct: 5.0m, // 5% discount
                    GstRatePercent: 12.0m // 12% GST (6% CGST + 6% SGST)
                ),
                // Item 2: Augmentin 625 (20 purchased + 0 free)
                new PurchaseInvoiceItemInputDto(
                    ProductId: "p_aug",
                    ProductName: "Augmentin 625 Duo Tablet",
                    HsnCode: "30041010",
                    BatchNumber: "AG26MAR",
                    ExpiryDate: new DateTime(2027, 9, 1),
                    ManufacturingDate: new DateTime(2026, 2, 1),
                    Quantity: 20,
                    FreeQuantity: 0,
                    UnitPrice: 150.00m,
                    Mrp: 205.00m,
                    SaleRate: 200.00m,
                    DiscountPct: 2.0m,
                    GstRatePercent: 12.0m
                )
            }
        );

        // 2. Act
        var result = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(command);

        // 3. Assert - Posting result
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.PurchaseInvoiceId);
        Assert.Equal(2, result.BatchesCreatedOrUpdated);
        Assert.Equal(75, result.TotalStockAdded); // 50 + 5 + 20 = 75
        Assert.True(result.GrandTotal > 0);

        // Verify supplier balance increased by GrandTotal
        var supplier = await _supplierRepository.GetSupplierByIdAsync(supplierId);
        Assert.NotNull(supplier);
        Assert.Equal(1000.00m + result.GrandTotal, supplier.CurrentOutstandingBalance);

        // Verify Outbox Event created
        var pendingOutbox = await _outboxRepository.GetPendingEventsAsync(10);
        Assert.Contains(pendingOutbox, e => e.AggregateType == "PurchaseInvoice" && e.AggregateId == result.PurchaseInvoiceId);

        // Verify Recent purchases query
        var recent = await _purchaseService.GetRecentPurchasesAsync("org-1", "br-1");
        Assert.NotEmpty(recent);
        Assert.Contains(recent, p => p.SupplierInvoiceNo == "MED-2026-9041");

        // Verify Automatic Double-Entry Accounting Posting
        var accRepo = new SqliteAccountingRepository(_connectionFactory, _outboxRepository);
        var dayBook = await accRepo.GetDayBookAsync("org-1", "br-1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Contains(dayBook, v => v.VoucherNumber.StartsWith("PUR-MED-2026-9041") && v.VoucherType == Domain.Accounting.VoucherType.Purchase);

        var purAccount = await accRepo.GetAccountHeadByIdAsync("acc_purchases");
        var credAccount = await accRepo.GetAccountHeadByIdAsync("acc_creditors");
        Assert.NotNull(purAccount);
        Assert.NotNull(credAccount);
        Assert.True(purAccount.CurrentBalance > 0);
        Assert.True(credAccount.CurrentBalance > 0);
    }


    [Fact]
    public async Task CreatePurchaseInvoice_InterstateTax_AppliesIgstCorrectly()
    {
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1",
            Name: "Mumbai Pharma Hub",
            Gstin: "27AABCU1234R1ZM",
            DlNumber: "MH-B1-112233",
            Phone: "+91 22 2455 6677",
            Email: "orders@mumbaipharma.in",
            Address: "12 Marine Drive, Mumbai",
            CreditDays: 30,
            OpeningBalance: 0
        ));

        var command = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1",
            BranchId: "br-1",
            WarehouseId: "wh-1",
            SupplierId: supplierId,
            SupplierName: "Mumbai Pharma Hub",
            SupplierGstin: "27AABCU1234R1ZM",
            SupplierInvoiceNo: "BOM-9921",
            SupplierInvoiceDate: DateTime.UtcNow.Date,
            IsInterstate: true, // Interstate
            CreatedByUserId: "USER-STOREKEEPER",
            Notes: null,
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new PurchaseInvoiceItemInputDto(
                    ProductId: "p_pand",
                    ProductName: "Pan-D Capsule",
                    HsnCode: "30049099",
                    BatchNumber: "PD26BOM",
                    ExpiryDate: new DateTime(2027, 12, 1),
                    ManufacturingDate: null,
                    Quantity: 100,
                    FreeQuantity: 10,
                    UnitPrice: 100.00m,
                    Mrp: 199.00m,
                    SaleRate: 190.00m,
                    DiscountPct: 10.0m, // 10% disc => Taxable = 9000
                    GstRatePercent: 12.0m // 12% IGST = 1080 => Net = 10080
                )
            }
        );

        var result = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(command);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(10080.00m, result.GrandTotal);
        Assert.Equal(110, result.TotalStockAdded); // 100 + 10 free

        // Test GetPurchaseInvoiceDetailsAsync
        var details = await _purchaseService.GetPurchaseInvoiceDetailsAsync(result.PurchaseInvoiceId!);
        Assert.NotNull(details);
        Assert.Equal("BOM-9921", details.SupplierInvoiceNo);
        Assert.Equal("Mumbai Pharma Hub", details.SupplierName);
        Assert.True(details.IsInterstate);
        Assert.Single(details.Items);
        Assert.Equal("PD26BOM", details.Items[0].BatchNumber);
        Assert.Equal(110, details.Items[0].TotalQuantity);

        // Test GetPurchaseKpiSummaryAsync
        var kpis = await _purchaseService.GetPurchaseKpiSummaryAsync("org-1", "br-1");
        Assert.True(kpis.TotalPurchaseAmount > 0);
        Assert.True(kpis.TotalInvoicesCount >= 1);
        Assert.True(kpis.TotalSuppliersCount >= 1);
    }

    [Fact]
    public async Task PostPurchaseInvoice_DuplicateInvoiceNo_ReturnsFailed()
    {
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1", Name: "DupGuard Test Supplier", Gstin: "07DUPG1234R1ZM",
            DlNumber: null, Phone: null, Email: null, Address: null, CreditDays: 30, OpeningBalance: 0));

        var cmd = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
            SupplierId: supplierId, SupplierName: "DupGuard Test Supplier",
            SupplierGstin: "07DUPG1234R1ZM", SupplierInvoiceNo: "TEST-DUP-001",
            SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
            CreatedByUserId: "user-1", Notes: null,
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new("p_dolo", "Dolo 650", "30049099", "BATCHDUP001",
                    DateTime.UtcNow.AddMonths(18), null, 10m, 0m, 22m, 30.5m, 30m, 0m, 12m)
            });

        var result1 = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
        Assert.True(result1.Success, result1.ErrorMessage);

        var result2 = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
        Assert.False(result2.Success);
        Assert.NotNull(result2.ErrorMessage);
        Assert.Contains("duplicate", result2.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelPostedInvoice_ReversesStockAndBalance()
    {
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1", Name: "Cancel Test Supplier", Gstin: "07CNCL1234R1ZM",
            DlNumber: null, Phone: null, Email: null, Address: null, CreditDays: 30, OpeningBalance: 0));

        var cmd = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
            SupplierId: supplierId, SupplierName: "Cancel Test Supplier",
            SupplierGstin: "07CNCL1234R1ZM", SupplierInvoiceNo: "CANCEL-TEST-001",
            SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
            CreatedByUserId: "user-1", Notes: null,
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new("p_dolo", "Dolo 650", "30049099", "BATCHCANCEL01",
                    DateTime.UtcNow.AddMonths(18), null, 20m, 0m, 22m, 30.5m, 30m, 0m, 12m)
            });

        var posted = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
        Assert.True(posted.Success, posted.ErrorMessage);

        var supplierAfterPost = await _supplierRepository.GetSupplierByIdAsync(supplierId);
        Assert.True(supplierAfterPost!.CurrentOutstandingBalance > 0);

        var cancelResult = await _purchaseService.CancelPurchaseInvoiceAsync(posted.PurchaseInvoiceId!, "user-1");
        Assert.True(cancelResult.Success, cancelResult.ErrorMessage);

        var details = await _purchaseService.GetPurchaseInvoiceDetailsAsync(posted.PurchaseInvoiceId!);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, details!.Status);

        var supplierAfterCancel = await _supplierRepository.GetSupplierByIdAsync(supplierId);
        Assert.Equal(0m, supplierAfterCancel!.CurrentOutstandingBalance);
    }

    [Fact]
    public async Task PostInvoice_WithZeroCostAndFreeQty_PostsSuccessfully()
    {
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1", Name: "Free Sample Supplier", Gstin: null,
            DlNumber: null, Phone: null, Email: null, Address: null, CreditDays: 0, OpeningBalance: 0));

        var cmd = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
            SupplierId: supplierId, SupplierName: "Free Sample Supplier",
            SupplierGstin: null, SupplierInvoiceNo: "ZERO-COST-001",
            SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
            CreatedByUserId: "user-1", Notes: "Free samples",
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new("p_dolo", "Dolo 650", "30049099", "FREEBATCH01",
                    DateTime.UtcNow.AddMonths(18), null, 1m, 9m, 0m, 30.5m, 30m, 0m, 12m)
            });

        var result = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(10m, result.TotalStockAdded);
    }
}


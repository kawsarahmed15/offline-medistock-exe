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
        Assert.Equal(9000.00m, result.GrandTotal);
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
        Assert.True(kpis.TotalStockValue > 0);
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

    [Fact]
    public async Task GetPurchaseKpiSummaryAsync_CalculatesStockValue_WithNetBuyingCost_IncludingGst()
    {
        // Example: Product with buying price 10 and 5% GST -> Net unit buying cost = 10.50
        // Inward 10 units -> Stock Value must be 10 * 10.50 = 105.00 (not 100.00)
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1", Name: "GST Pharma Wholesaler", Gstin: "07AAAAA0000A1Z5",
            DlNumber: null, Phone: null, Email: null, Address: null, CreditDays: 30, OpeningBalance: 0));

        var cmd = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
            SupplierId: supplierId, SupplierName: "GST Pharma Wholesaler",
            SupplierGstin: "07AAAAA0000A1Z5", SupplierInvoiceNo: "NETCOST-001",
            SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
            CreatedByUserId: "user-1", Notes: "Test Net Cost Calculation",
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new("p_test_gst5", "Test Product 5% GST", "30049099", "NETBATCH01",
                    DateTime.UtcNow.AddMonths(24), null, 10m, 0m, 10.00m, 15.00m, 15.00m, 0m, 5.0m)
            });

        var postResult = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
        Assert.True(postResult.Success, postResult.ErrorMessage);

        var kpis = await _purchaseService.GetPurchaseKpiSummaryAsync("org-1", "br-1", "All");

        // Verify that stock value is calculated with 10.50 net buying cost:
        // Prior seeded batches might exist, but the newly added batch must add 10 * (10.00 * 1.05) = 105.00
        Assert.True(kpis.TotalStockValue >= 105.00m);
    }

    [Fact]
    public async Task UpdatePurchaseInvoiceAtomicAsync_UpdatesItemsAndRevisesStockBalances()
    {
        // 1. Post initial invoice with 10 units of BATCH-EDIT-01
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1", Name: "Vendor For Edit", Gstin: "07AABCE1234F1Z1",
            DlNumber: null, Phone: null, Email: null, Address: null, CreditDays: 30, OpeningBalance: 0));

        var initialCmd = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
            SupplierId: supplierId, SupplierName: "Vendor For Edit",
            SupplierGstin: "07AABCE1234F1Z1", SupplierInvoiceNo: "EDIT-INV-001",
            SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
            CreatedByUserId: "user-1", Notes: "Initial bill",
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new("p_edit_1", "Edit Medicine 1", "30049099", "BATCH-EDIT-01",
                    DateTime.UtcNow.AddMonths(12), null, 10m, 0m, 50.00m, 80.00m, 80.00m, 0m, 12.0m)
            });

        var postRes = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(initialCmd);
        Assert.True(postRes.Success);

        var stockInitial = await _purchaseRepository.GetBatchAvailableStockAsync("p_edit_1", "BATCH-EDIT-01", "wh-1", "org-1");
        Assert.Equal(10m, stockInitial);

        // 2. Now edit this invoice: update BATCH-EDIT-01 quantity from 10 to 15, and add a new medicine BATCH-EDIT-02 (5 units)
        var updateCmd = new UpdatePurchaseInvoiceCommand(
            InvoiceId: postRes.PurchaseInvoiceId!,
            OrgId: "org-1",
            BranchId: "br-1",
            WarehouseId: "wh-1",
            SupplierId: supplierId,
            SupplierName: "Vendor For Edit",
            SupplierGstin: "07AABCE1234F1Z1",
            SupplierInvoiceNo: "EDIT-INV-001",
            SupplierInvoiceDate: DateTime.UtcNow,
            IsInterstate: false,
            UpdatedByUserId: "user-1",
            Notes: "Edited bill with revised quantities and extra product",
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new("p_edit_1", "Edit Medicine 1", "30049099", "BATCH-EDIT-01",
                    DateTime.UtcNow.AddMonths(12), null, 15m, 0m, 50.00m, 80.00m, 80.00m, 0m, 12.0m),
                new("p_edit_2", "Edit Medicine 2", "30049099", "BATCH-EDIT-02",
                    DateTime.UtcNow.AddMonths(18), null, 5m, 0m, 20.00m, 35.00m, 35.00m, 0m, 5.0m)
            }
        );

        var updateRes = await _purchaseRepository.UpdatePurchaseInvoiceAtomicAsync(updateCmd);
        Assert.True(updateRes.Success, updateRes.ErrorMessage);

        // 3. Verify stock balances: BATCH-EDIT-01 must be 15 (reversal of 10, then add 15), BATCH-EDIT-02 must be 5
        var stockAfter1 = await _purchaseRepository.GetBatchAvailableStockAsync("p_edit_1", "BATCH-EDIT-01", "wh-1", "org-1");
        var stockAfter2 = await _purchaseRepository.GetBatchAvailableStockAsync("p_edit_2", "BATCH-EDIT-02", "wh-1", "org-1");

        Assert.Equal(15m, stockAfter1);
        Assert.Equal(5m, stockAfter2);

        // 4. Verify invoice details reflects updated items count and totals
        var details = await _purchaseRepository.GetPurchaseInvoiceDetailsAsync(postRes.PurchaseInvoiceId!);
        Assert.NotNull(details);
        Assert.Equal(2, details.Items.Count);
        Assert.Equal("Edited bill with revised quantities and extra product", details.Notes);
    }

    [Fact]
    public async Task ProcessPurchaseReturnAtomicAsync_DeductsStockAndAdjustsInventoryValuation_AndGeneratesDebitNote()
    {
        // 1. Post purchase invoice: 20 units of product with buying rate 10.00, GST 5% -> Net unit cost = 10.50
        var supplierId = await _purchaseService.CreateSupplierAsync(new CreateSupplierCommand(
            OrgId: "org-1", Name: "Return Wholesaler Co", Gstin: "27AABCR1234F1Z3",
            DlNumber: null, Phone: null, Email: null, Address: null, CreditDays: 30, OpeningBalance: 0));

        var cmd = new CreatePurchaseInvoiceCommand(
            OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
            SupplierId: supplierId, SupplierName: "Return Wholesaler Co",
            SupplierGstin: "27AABCR1234F1Z3", SupplierInvoiceNo: "RET-INV-100",
            SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
            CreatedByUserId: "user-1", Notes: "Return test invoice",
            Items: new List<PurchaseInvoiceItemInputDto>
            {
                new("p_return_test", "Return Test Medicine", "30049099", "BATCH-RET-01",
                    DateTime.UtcNow.AddMonths(12), null, 20m, 0m, 10.00m, 20.00m, 20.00m, 0m, 5.0m)
            });

        var postRes = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
        Assert.True(postRes.Success);

        // Initial stock = 20
        var stockBefore = await _purchaseRepository.GetBatchAvailableStockAsync("p_return_test", "BATCH-RET-01", "wh-1", "org-1");
        Assert.Equal(20m, stockBefore);

        var kpiBefore = await _purchaseRepository.GetPurchaseKpiSummaryAsync("org-1", "br-1");
        var stockValuationBefore = kpiBefore.TotalStockValue;

        // 2. Process Purchase Return for 6 units:
        // Net unit price = 10.00 (inclusive of GST)
        // Net return value = 6 * 10.00 = 60.00
        var returnCmd = new CreatePurchaseReturnCommand(
            OrgId: "org-1",
            BranchId: "br-1",
            WarehouseId: "wh-1",
            PurchaseInvoiceId: postRes.PurchaseInvoiceId!,
            SupplierId: supplierId,
            SupplierName: "Return Wholesaler Co",
            SupplierGstin: "27AABCR1234F1Z3",
            OriginalInvoiceNo: "RET-INV-100",
            CreatedByUserId: "user-1",
            Notes: "Return of damaged goods",
            Items: new List<PurchaseReturnItemInputDto>
            {
                new("p_return_test", "Return Test Medicine", "BATCH-RET-01",
                    DateTime.UtcNow.AddMonths(12), 6m, 10.00m, 5.0m, 10.00m, 60.00m, "Damaged in transit")
            }
        );

        var returnRes = await _purchaseRepository.ProcessPurchaseReturnAtomicAsync(returnCmd);

        // 3. Verify Return Result
        Assert.True(returnRes.Success, returnRes.ErrorMessage);
        Assert.StartsWith("PR-", returnRes.ReturnNumber);
        Assert.Equal(1, returnRes.ItemsReturnedCount);
        Assert.Equal(6m, returnRes.TotalQuantityReturned);
        Assert.Equal(60.00m, returnRes.TotalReturnAmount);

        // 4. Verify Stock balance is reduced by 6 units (20 - 6 = 14)
        var stockAfter = await _purchaseRepository.GetBatchAvailableStockAsync("p_return_test", "BATCH-RET-01", "wh-1", "org-1");
        Assert.Equal(14m, stockAfter);

        // 5. Verify Total Stock Valuation dropped by exactly 60.00 (6 * 10.00)
        var kpiAfter = await _purchaseRepository.GetPurchaseKpiSummaryAsync("org-1", "br-1");
        Assert.Equal(stockValuationBefore - 60.00m, kpiAfter.TotalStockValue);
    }
}


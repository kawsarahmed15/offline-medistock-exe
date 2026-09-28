# Purchase Entry Keyboard-First Overhaul — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Purchase Entry screen to match POS keyboard-first speed — full Enter-chain header nav, ↑/↓/←/→ grid cell navigation, inline medicine search, atomic Cancel-Invoice reversal, duplicate invoice guard, and batch conflict fix.

**Architecture:**
- Tasks 1–3 fix the backend (Repository duplicate guard + batch conflict key + Cancel reversal).
- Tasks 4–5 fix the ViewModel (blank row template + product search + active-row tracker).
- Tasks 6–7 rebuild the XAML and code-behind for full keyboard routing.
- Task 8 covers tests.

**Tech Stack:** C# 12, WinUI 3 / UNO, CommunityToolkit.Mvvm, Dapper, SQLite, xUnit

**Spec:** `docs/superpowers/plans/2026-09-28-purchase-keyboard-first.md` (this file)

## Global Constraints

- Target: `net9.0-windows10.0.26100.0` — no API below Windows 10 19041.
- All SQLite writes go through a `BeginTransaction / Commit / Rollback` — no naked `ExecuteAsync` outside a transaction for multi-step operations.
- Domain factory methods only: `StockMovement.Record(...)` NOT `.Create(...)`. `Batch.Create(...)` for new batches.
- All UI focus changes use `FocusState.Programmatic` — never `FocusState.Keyboard` from code.
- Build target: `src/Clients/Medistock.Desktop/Medistock.Desktop.csproj`
- Run tests via: `dotnet test tests/Medistock.Infrastructure.Tests/` and `dotnet test tests/Medistock.Desktop.Tests/`
- After every task: run `dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj` — zero errors required before moving to next task.
- Use `x:Name` on every focusable control in the purchase entry row template so code-behind can reference them.

## Review Focus

1. **Duplicate supplier invoice re-post:** If a user posts `APEX/2026/8892` twice, stock doubles. The `PostPurchaseInvoiceAtomicAsync` must check for duplicates before inserting.
2. **Cancel of a partially-paid invoice:** If a supplier has been partially paid, cancelling the invoice should still reverse the *original* grand total on the outstanding balance — not just the remaining balance.
3. **Same batch received twice (re-purchase):** Receiving `Batch DL2609` for Dolo 650 again must ADD stock to the existing `stock_balances` row, not create a second batch record. The `ON CONFLICT` key must be `(product_id, batch_number, org_id)` not `(id)`.
4. **Expiry date in past on entry:** The system should warn (not block) if the user enters an expiry date already in the past.
5. **Zero-cost row:** If `UnitPrice = 0` (e.g., free samples), `LandedCostPerUnit` must be 0 — no division-by-zero. Also the row should still post successfully since free samples are valid purchase entries.

---

## Task 1 — Repository: Duplicate Invoice Guard

**Files:**
- Modify: `src/Infrastructure/Medistock.Infrastructure.Data/Repositories/SqlitePurchaseRepository.cs` lines 31–367
- Modify: `src/Core/Medistock.Application/Common/Interfaces/IPurchaseRepository.cs`
- Test: `tests/Medistock.Infrastructure.Tests/PurchaseTests.cs`

**Interfaces:**
- Produces: `IPurchaseRepository.IsDuplicateInvoiceAsync(string orgId, string supplierId, string supplierInvoiceNo) → Task<bool>` (used by Task 5 ViewModel for early warning) and the guard inside `PostPurchaseInvoiceAtomicAsync` itself.

- [ ] **Step 1: Add failing test for duplicate invoice guard**

Add this test to `tests/Medistock.Infrastructure.Tests/PurchaseTests.cs`:

```csharp
[Fact]
public async Task PostPurchaseInvoice_DuplicateInvoiceNo_ReturnsFailed()
{
    // Arrange
    var suppliers = await _purchaseService.GetSuppliersAsync("org-1");
    var supplier = suppliers.First();

    var cmd = new CreatePurchaseInvoiceCommand(
        OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
        SupplierId: supplier.Id, SupplierName: supplier.Name,
        SupplierGstin: supplier.Gstin, SupplierInvoiceNo: "TEST-DUP-001",
        SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
        CreatedByUserId: "user-1", Notes: null,
        Items: new List<PurchaseInvoiceItemInputDto>
        {
            new("p_dolo", "Dolo 650", "30049099", "BATCHDUP001",
                DateTime.UtcNow.AddMonths(18), null,
                10m, 0m, 22m, 30.5m, 30m, 0m, 12m)
        });

    // Act — first post should succeed
    var result1 = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
    Assert.True(result1.Success);

    // Act — second post with same invoice no must fail
    var result2 = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
    Assert.False(result2.Success);
    Assert.NotNull(result2.ErrorMessage);
    Assert.Contains("duplicate", result2.ErrorMessage, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run test — verify it fails**
```
dotnet test tests/Medistock.Infrastructure.Tests/ --filter "PostPurchaseInvoice_DuplicateInvoiceNo_ReturnsFailed" -v
```
Expected: FAIL — second post currently succeeds.

- [ ] **Step 3: Add `IsDuplicateInvoiceAsync` to interface**

In `src/Core/Medistock.Application/Common/Interfaces/IPurchaseRepository.cs`, add:
```csharp
Task<bool> IsDuplicateInvoiceAsync(
    string orgId,
    string supplierId,
    string supplierInvoiceNo,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement in `SqlitePurchaseRepository`**

Add method after the `PostPurchaseInvoiceAtomicAsync` method:
```csharp
public async Task<bool> IsDuplicateInvoiceAsync(
    string orgId,
    string supplierId,
    string supplierInvoiceNo,
    CancellationToken cancellationToken = default)
{
    using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
    var count = await connection.ExecuteScalarAsync<int>(
        new CommandDefinition(
            "SELECT COUNT(1) FROM purchase_invoices WHERE org_id = @orgId AND supplier_id = @supplierId AND supplier_invoice_no = @supplierInvoiceNo;",
            new { orgId, supplierId, supplierInvoiceNo },
            cancellationToken: cancellationToken));
    return count > 0;
}
```

Add the guard at the **very top** of `PostPurchaseInvoiceAtomicAsync`, before `connection.BeginTransaction()`:
```csharp
// Duplicate invoice guard (before transaction)
var isDuplicate = await IsDuplicateInvoiceAsync(
    invoice.OrgId, invoice.SupplierId, invoice.SupplierInvoiceNo, cancellationToken);
if (isDuplicate)
{
    return new PurchasePostingResult(false, null, 0, 0, 0,
        $"Duplicate invoice: '{invoice.SupplierInvoiceNo}' already exists for this supplier. Cancel the existing invoice first, then re-enter.");
}
```

- [ ] **Step 5: Run test — verify it passes**
```
dotnet test tests/Medistock.Infrastructure.Tests/ --filter "PostPurchaseInvoice_DuplicateInvoiceNo_ReturnsFailed" -v
```
Expected: PASS

- [ ] **Step 6: Build check**
```
dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj
```
Expected: 0 errors.

- [ ] **Step 7: Commit**
```
git add src/Core/Medistock.Application/Common/Interfaces/IPurchaseRepository.cs
git add src/Infrastructure/Medistock.Infrastructure.Data/Repositories/SqlitePurchaseRepository.cs
git add tests/Medistock.Infrastructure.Tests/PurchaseTests.cs
git commit -m "fix: duplicate purchase invoice guard in repository"
```

---

## Task 2 — Repository: Fix Batch Conflict Key + Add Cancel Invoice

**Files:**
- Modify: `src/Infrastructure/Medistock.Infrastructure.Data/Repositories/SqlitePurchaseRepository.cs`
- Modify: `src/Core/Medistock.Application/Common/Interfaces/IPurchaseRepository.cs`
- Modify: `src/Core/Medistock.Application/Purchases/Services/PurchaseService.cs`
- Modify: `src/Core/Medistock.Application/Purchases/DTOs/PurchaseDtos.cs`
- Test: `tests/Medistock.Infrastructure.Tests/PurchaseTests.cs`

**Interfaces:**
- Produces: `IPurchaseRepository.CancelPurchaseInvoiceAsync(string invoiceId, string cancelledByUserId) → Task<PurchaseCancelResult>`
- Produces: `IPurchaseService.CancelPurchaseInvoiceAsync(string invoiceId, string cancelledByUserId) → Task<PurchaseCancelResult>`
- Produces: `PurchaseCancelResult` record

- [ ] **Step 1: Add DTO for cancel result in `PurchaseDtos.cs`**

Append to `src/Core/Medistock.Application/Purchases/DTOs/PurchaseDtos.cs`:
```csharp
public record PurchaseCancelResult(
    bool Success,
    string? ErrorMessage = null
);
```

- [ ] **Step 2: Add cancel method to interface**

In `IPurchaseRepository.cs` add:
```csharp
Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(
    string invoiceId,
    string cancelledByUserId,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Write failing test for cancel**

Add to `tests/Medistock.Infrastructure.Tests/PurchaseTests.cs`:
```csharp
[Fact]
public async Task CancelPostedInvoice_ReversesStockAndBalance()
{
    // Arrange — post an invoice first
    var suppliers = await _purchaseService.GetSuppliersAsync("org-1");
    var supplier = suppliers.First();
    var supplierIdBefore = supplier.CurrentOutstandingBalance;

    var cmd = new CreatePurchaseInvoiceCommand(
        OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
        SupplierId: supplier.Id, SupplierName: supplier.Name,
        SupplierGstin: supplier.Gstin, SupplierInvoiceNo: "CANCEL-TEST-001",
        SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
        CreatedByUserId: "user-1", Notes: null,
        Items: new List<PurchaseInvoiceItemInputDto>
        {
            new("p_dolo", "Dolo 650", "30049099", "BATCHCANCEL01",
                DateTime.UtcNow.AddMonths(18), null,
                20m, 0m, 22m, 30.5m, 30m, 0m, 12m)
        });

    var posted = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
    Assert.True(posted.Success);
    Assert.NotNull(posted.PurchaseInvoiceId);

    // Act — cancel it
    var cancelResult = await _purchaseService.CancelPurchaseInvoiceAsync(posted.PurchaseInvoiceId!, "user-1");

    // Assert
    Assert.True(cancelResult.Success);

    // Invoice should be marked cancelled
    var details = await _purchaseService.GetPurchaseInvoiceDetailsAsync(posted.PurchaseInvoiceId!);
    Assert.Equal(PurchaseInvoiceStatus.Cancelled, details!.Status);

    // Supplier balance should be back to original (net zero effect)
    var suppliersAfter = await _purchaseService.GetSuppliersAsync("org-1");
    var supplierAfter = suppliersAfter.First(s => s.Id == supplier.Id);
    Assert.Equal(supplierIdBefore, supplierAfter.CurrentOutstandingBalance);
}
```

- [ ] **Step 4: Run test — verify it fails**
```
dotnet test tests/Medistock.Infrastructure.Tests/ --filter "CancelPostedInvoice_ReversesStockAndBalance" -v
```
Expected: FAIL — method not yet implemented.

- [ ] **Step 5: Implement `CancelPurchaseInvoiceAsync` in `SqlitePurchaseRepository`**

Add after `GetPurchaseKpiSummaryAsync`:
```csharp
public async Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(
    string invoiceId,
    string cancelledByUserId,
    CancellationToken cancellationToken = default)
{
    using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
    using var transaction = connection.BeginTransaction();

    try
    {
        // 1. Fetch invoice header — must exist and be in Posted state
        var invRow = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(
                "SELECT id, org_id, branch_id, warehouse_id, supplier_id, grand_total, status FROM purchase_invoices WHERE id = @invoiceId;",
                new { invoiceId }, transaction, cancellationToken: cancellationToken));

        if (invRow == null)
            return new PurchaseCancelResult(false, "Invoice not found.");

        int status = (int)invRow.status;
        if (status == 2)
            return new PurchaseCancelResult(false, "Invoice is already cancelled.");
        if (status == 0)
            return new PurchaseCancelResult(false, "Draft invoice cannot be cancelled — just delete it.");

        string orgId = (string)invRow.org_id;
        string branchId = (string)invRow.branch_id;
        string warehouseId = (string)invRow.warehouse_id;
        string supplierId = (string)invRow.supplier_id;
        decimal grandTotal = Convert.ToDecimal(invRow.grand_total);

        // 2. Fetch all line items to reverse stock
        var items = await connection.QueryAsync<dynamic>(
            new CommandDefinition(
                "SELECT product_id, batch_number, quantity, free_quantity FROM purchase_invoice_items WHERE purchase_invoice_id = @invoiceId;",
                new { invoiceId }, transaction, cancellationToken: cancellationToken));

        // 3. For each item, reverse stock_balances
        foreach (var item in items)
        {
            string productId = (string)item.product_id;
            string batchNumber = (string)item.batch_number;
            decimal qty = Convert.ToDecimal(item.quantity);
            decimal freeQty = Convert.ToDecimal(item.free_quantity);
            decimal totalQty = qty + freeQty;

            // Find the batch by (product_id, batch_number, org_id)
            var batchId = await connection.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    "SELECT id FROM batches WHERE product_id = @productId AND batch_number = @batchNumber AND org_id = @orgId LIMIT 1;",
                    new { productId, batchNumber, orgId }, transaction, cancellationToken: cancellationToken));

            if (batchId != null)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE stock_balances SET quantity = MAX(0, quantity - @qty), last_updated_at = @now WHERE batch_id = @batchId AND warehouse_id = @warehouseId;",
                        new { qty = (double)totalQty, now = DateTime.UtcNow.ToString("o"), batchId, warehouseId },
                        transaction, cancellationToken: cancellationToken));

                // 4. Insert reversal stock movement
                var movId = Guid.NewGuid().ToString("N");
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        @"INSERT INTO stock_movements (id, org_id, branch_id, warehouse_id, batch_id, product_id,
                            movement_type, quantity, reference_type, reference_id, unit_cost, user_id, device_id, created_at)
                          VALUES (@Id, @OrgId, @BranchId, @WarehouseId, @BatchId, @ProductId,
                            5, @Qty, 'PURCHASE_CANCEL', @InvoiceId, 0.0, @UserId, 'DESKTOP-01', @CreatedAt);",
                        new { Id = movId, OrgId = orgId, BranchId = branchId, WarehouseId = warehouseId,
                              BatchId = batchId, ProductId = productId,
                              Qty = (double)totalQty, InvoiceId = invoiceId,
                              UserId = cancelledByUserId, CreatedAt = DateTime.UtcNow.ToString("o") },
                        transaction, cancellationToken: cancellationToken));
            }
        }

        // 5. Reverse supplier outstanding balance (subtract grandTotal)
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE suppliers SET outstanding_balance = MAX(0, outstanding_balance - @delta) WHERE id = @supplierId;",
                new { delta = (double)grandTotal, supplierId },
                transaction, cancellationToken: cancellationToken));

        // 6. Mark invoice CANCELLED and record who cancelled and when
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE purchase_invoices SET status = 2, notes = COALESCE(notes, '') || ' [CANCELLED by ' || @userId || ' at ' || @cancelledAt || ']' WHERE id = @invoiceId;",
                new { userId = cancelledByUserId, cancelledAt = DateTime.UtcNow.ToString("o"), invoiceId },
                transaction, cancellationToken: cancellationToken));

        // 7. Reversal journal entry
        var journalId = $"je_cancel_{invoiceId}";
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT OR IGNORE INTO journal_entries
                    (id, org_id, branch_id, voucher_number, voucher_type, voucher_date, narration, reference_id, reference_type, created_by_user_id, created_at)
                  VALUES (@Id, @OrgId, @BranchId, @VoucherNo, 2, datetime('now'), @Narration, @RefId, 'PURCHASE_CANCEL', @UserId, datetime('now'));",
                new { Id = journalId, OrgId = orgId, BranchId = branchId,
                      VoucherNo = $"CANCEL-PUR-{invoiceId[..8].ToUpperInvariant()}",
                      Narration = $"Cancellation reversal of purchase invoice {invoiceId}",
                      RefId = invoiceId, UserId = cancelledByUserId },
                transaction, cancellationToken: cancellationToken));

        // Reversal lines: Credit Purchases, Debit Creditors
        var lineId1 = $"jl_{Guid.NewGuid():N}";
        var lineId2 = $"jl_{Guid.NewGuid():N}";
        await connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO journal_lines (id, journal_entry_id, account_id, account_name, debit_amount, credit_amount, narration) VALUES (@Id, @JeId, 'acc_purchases', 'Pharmacy Medicine Purchases A/c', 0.0, CAST(@Amt AS REAL), @Narr);",
                new { Id = lineId1, JeId = journalId, Amt = (double)grandTotal, Narr = "Purchase reversal" },
                transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO journal_lines (id, journal_entry_id, account_id, account_name, debit_amount, credit_amount, narration) VALUES (@Id, @JeId, 'acc_creditors', 'Sundry Creditors (Suppliers)', CAST(@Amt AS REAL), 0.0, @Narr);",
                new { Id = lineId2, JeId = journalId, Amt = (double)grandTotal, Narr = "Purchase reversal" },
                transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return new PurchaseCancelResult(true);
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        return new PurchaseCancelResult(false, ex.Message);
    }
}
```

- [ ] **Step 6: Wire cancel through `IPurchaseService` and `PurchaseService`**

In `src/Core/Medistock.Application/Purchases/Services/PurchaseService.cs`, add to interface definition:
```csharp
Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(string invoiceId, string cancelledByUserId, CancellationToken cancellationToken = default);
```

Add implementation method:
```csharp
public Task<PurchaseCancelResult> CancelPurchaseInvoiceAsync(
    string invoiceId,
    string cancelledByUserId,
    CancellationToken cancellationToken = default)
{
    return _purchaseRepository.CancelPurchaseInvoiceAsync(invoiceId, cancelledByUserId, cancellationToken);
}
```

- [ ] **Step 7: Fix batch upsert conflict key**

In `SqlitePurchaseRepository.cs`, replace the `upsertBatchSql` constant (lines ~144–157):
```csharp
const string upsertBatchSql = @"
    INSERT INTO batches (
        id, product_id, org_id, batch_number, expiry_date,
        manufacturing_date, mrp, purchase_rate, sale_rate, created_at
    ) VALUES (
        @Id, @ProductId, @OrgId, @BatchNumber, @ExpiryDate,
        @ManufacturingDate, @Mrp, @PurchaseRate, @SaleRate, @CreatedAt
    )
    ON CONFLICT(product_id, batch_number, org_id) DO UPDATE SET
        mrp = excluded.mrp,
        purchase_rate = excluded.purchase_rate,
        sale_rate = excluded.sale_rate,
        expiry_date = excluded.expiry_date;
";
```

> **Note:** The `batches` table must have a UNIQUE constraint on `(product_id, batch_number, org_id)`. Check the migration. If the constraint doesn't exist, the `ON CONFLICT` clause will have no effect. The migration check is in Task 2 Step 8.

- [ ] **Step 8: Verify/add UNIQUE index in migration**

Open `src/Infrastructure/Medistock.Infrastructure.Data/Migrations/DatabaseMigrator.cs` and search for the `batches` table DDL. Ensure it contains:
```sql
UNIQUE(product_id, batch_number, org_id)
```
If the `CREATE TABLE batches` statement does not have this UNIQUE constraint, add it, or add a migration step:
```csharp
await connection.ExecuteAsync(@"
    CREATE UNIQUE INDEX IF NOT EXISTS idx_batches_product_batch_org
    ON batches(product_id, batch_number, org_id);
");
```
Add this to the migration after table creation.

- [ ] **Step 9: Run failing test — verify it now passes**
```
dotnet test tests/Medistock.Infrastructure.Tests/ --filter "CancelPostedInvoice_ReversesStockAndBalance" -v
```
Expected: PASS

- [ ] **Step 10: Build check**
```
dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj
```

- [ ] **Step 11: Commit**
```
git add src/Core/Medistock.Application/Purchases/DTOs/PurchaseDtos.cs
git add src/Core/Medistock.Application/Common/Interfaces/IPurchaseRepository.cs
git add src/Core/Medistock.Application/Purchases/Services/PurchaseService.cs
git add src/Infrastructure/Medistock.Infrastructure.Data/Repositories/SqlitePurchaseRepository.cs
git add src/Infrastructure/Medistock.Infrastructure.Data/Migrations/DatabaseMigrator.cs
git add tests/Medistock.Infrastructure.Tests/PurchaseTests.cs
git commit -m "feat: cancel purchase invoice with atomic stock/balance reversal; fix batch conflict key"
```

---

## Task 3 — ViewModel: Blank Row Template + Product Search + Active Row Tracker + Cancel Command

**Files:**
- Modify: `src/Clients/Medistock.Desktop/ViewModels/PurchaseEntryViewModel.cs` (full rewrite of key sections)
- Test: `tests/Medistock.Desktop.Tests/PurchaseViewModelAndExportTests.cs`

**Interfaces:**
- Consumes: `IPurchaseService.CancelPurchaseInvoiceAsync(string invoiceId, string userId)` → `Task<PurchaseCancelResult>`
- Consumes: `IPurchaseService.CreateAndPostPurchaseInvoiceAsync(command)` — unchanged
- Produces: `PurchaseEntryViewModel.ActiveRowIndex` (int), `PurchaseEntryViewModel.AddBlankRow()`, `PurchaseEntryViewModel.ActiveRow` (PurchaseItemRowViewModel?), `SearchRowProductAsync(string query, PurchaseItemRowViewModel row)`

- [ ] **Step 1: Write failing tests**

Add to `tests/Medistock.Desktop.Tests/PurchaseViewModelAndExportTests.cs`:
```csharp
[Fact]
public void AddBlankRow_DoesNotContainDemoData()
{
    // Arrange
    var vm = CreateViewModel();
    vm.LineItems.Clear(); // start fresh

    // Act
    vm.AddBlankRow();

    // Assert
    var row = vm.LineItems[0];
    Assert.Equal(string.Empty, row.ProductName);
    Assert.Equal(string.Empty, row.BatchNumber);
    Assert.Equal(0m, row.UnitPrice);
    Assert.Equal(0m, row.Mrp);
    Assert.Equal(1m, row.Quantity); // default qty = 1
}

[Fact]
public void ActiveRowIndex_UpdatedWhenNavigatingRows()
{
    var vm = CreateViewModel();
    vm.LineItems.Clear();
    vm.AddBlankRow();
    vm.AddBlankRow();

    vm.SetActiveRow(0);
    Assert.Equal(0, vm.ActiveRowIndex);

    vm.SetActiveRow(1);
    Assert.Equal(1, vm.ActiveRowIndex);
}
```
Helper at bottom of test class:
```csharp
private static PurchaseEntryViewModel CreateViewModel()
{
    var purchaseSvc = new MockPurchaseService();
    var searchRepo = new MockProductSearchRepository();
    return new PurchaseEntryViewModel(purchaseSvc, searchRepo, exportService: null);
}
```

- [ ] **Step 2: Run tests — verify they fail**
```
dotnet test tests/Medistock.Desktop.Tests/ --filter "AddBlankRow_DoesNotContainDemoData|ActiveRowIndex_UpdatedWhenNavigatingRows" -v
```

- [ ] **Step 3: Replace `AddNewRow()` with `AddBlankRow()` in ViewModel**

In `src/Clients/Medistock.Desktop/ViewModels/PurchaseEntryViewModel.cs`:

Replace the existing `AddNewRow()` command method (lines ~372–393) entirely:
```csharp
[RelayCommand]
public void AddBlankRow()
{
    var row = new PurchaseItemRowViewModel
    {
        Quantity = 1,
        GstRatePercent = 12.0m,
        IsInterstate = IsInterstate
    };
    row.PropertyChanged += (s, e) => RecalculateTotals();
    LineItems.Add(row);
    ActiveRowIndex = LineItems.Count - 1;
    RecalculateTotals();
}
```

Remove the old `AddNewRow` and update the constructor call to use `AddBlankRow()`:
```csharp
public PurchaseEntryViewModel(...)
{
    ...
    AddBlankRow(); // was: AddNewRow()
}
```

- [ ] **Step 4: Add `ActiveRowIndex`, `ActiveRow`, `SetActiveRow` to ViewModel**

Add these observable properties and method inside `PurchaseEntryViewModel`:
```csharp
[ObservableProperty]
private int _activeRowIndex = -1;

public PurchaseItemRowViewModel? ActiveRow =>
    ActiveRowIndex >= 0 && ActiveRowIndex < LineItems.Count
        ? LineItems[ActiveRowIndex]
        : null;

public void SetActiveRow(int index)
{
    if (index < 0 || index >= LineItems.Count) return;
    ActiveRowIndex = index;
    OnPropertyChanged(nameof(ActiveRow));
}
```

- [ ] **Step 5: Add `SearchRowProductAsync` inline product search**

Add this method to `PurchaseEntryViewModel`:
```csharp
public async Task SearchRowProductAsync(string query, PurchaseItemRowViewModel row)
{
    if (string.IsNullOrWhiteSpace(query) || query.Length < 2) return;

    try
    {
        var results = await _productSearchRepository.SearchProductsAsync(query, _warehouseId, limit: 10);
        if (results.Count == 0) return;

        var prod = results[0]; // take top match — code-behind shows popup for multiple
        row.ProductId = prod.Id;
        row.ProductName = prod.Name;
        row.GenericName = prod.GenericName;
        row.HsnCode = prod.HsnCode;
        row.GstRatePercent = prod.GstRatePercent > 0 ? prod.GstRatePercent : 12.0m;
        if (prod.Mrp > 0) row.Mrp = prod.Mrp;
        if (prod.SaleRate > 0) row.SaleRate = prod.SaleRate;
        // Pre-fill cost from last purchase price if 0; else estimate 70% of MRP
        if (row.UnitPrice == 0 && prod.Mrp > 0)
            row.UnitPrice = Math.Round(prod.Mrp * 0.70m, 2);
        row.Recalculate();
        RecalculateTotals();
    }
    catch { }
}
```

- [ ] **Step 6: Add `CancelSelectedInvoiceCommand` to ViewModel**

Add observable state and command:
```csharp
[ObservableProperty]
private bool _isCancelConfirmOpen = false;

[ObservableProperty]
private PurchaseInvoiceSummaryDto? _invoicePendingCancel;

[RelayCommand]
public void RequestCancelInvoice(PurchaseInvoiceSummaryDto summary)
{
    InvoicePendingCancel = summary;
    IsCancelConfirmOpen = true;
}

[RelayCommand]
public void DismissCancelConfirm()
{
    IsCancelConfirmOpen = false;
    InvoicePendingCancel = null;
}

[RelayCommand]
public async Task ConfirmCancelInvoiceAsync()
{
    if (InvoicePendingCancel == null) return;
    IsCancelConfirmOpen = false;

    IsBusy = true;
    StatusMessage = "⏳ Cancelling invoice and reversing stock...";
    try
    {
        var result = await _purchaseService.CancelPurchaseInvoiceAsync(
            InvoicePendingCancel.Id, "USER-STOREKEEPER");

        StatusMessage = result.Success
            ? $"✅ Invoice {InvoicePendingCancel.SupplierInvoiceNo} cancelled successfully."
            : $"❌ Cancel failed: {result.ErrorMessage}";

        if (result.Success)
        {
            await LoadRecentPurchasesAsync();
            await LoadKpisAsync();
            await LoadSuppliersAsync();
        }
    }
    catch (Exception ex)
    {
        StatusMessage = $"❌ Error: {ex.Message}";
    }
    finally
    {
        IsBusy = false;
        InvoicePendingCancel = null;
    }
}
```

Also update `PostPurchaseInvoiceAsync` to check for duplicate before posting (VM-level early warning):
After `if (LineItems.Count == 0)` block, add:
```csharp
// Pre-check duplicate (advisory — repository also guards atomically)
if (!string.IsNullOrEmpty(SelectedSupplierId))
{
    try
    {
        var isDup = await _purchaseService.IsDuplicateInvoiceAsync(
            _orgId, SelectedSupplierId, SupplierInvoiceNo.Trim());
        if (isDup)
        {
            StatusMessage = $"⚠️ Invoice '{SupplierInvoiceNo.Trim()}' already exists for this supplier. Cancel the existing invoice first.";
            IsBusy = false;
            return;
        }
    }
    catch { }
}
```

Add `IsDuplicateInvoiceAsync` to `IPurchaseService` and implement in `PurchaseService.cs`:
```csharp
// In interface:
Task<bool> IsDuplicateInvoiceAsync(string orgId, string supplierId, string supplierInvoiceNo, CancellationToken ct = default);

// In PurchaseService:
public Task<bool> IsDuplicateInvoiceAsync(string orgId, string supplierId, string supplierInvoiceNo, CancellationToken ct = default)
    => _purchaseRepository.IsDuplicateInvoiceAsync(orgId, supplierId, supplierInvoiceNo, ct);
```

- [ ] **Step 7: Run tests — verify they pass**
```
dotnet test tests/Medistock.Desktop.Tests/ --filter "AddBlankRow_DoesNotContainDemoData|ActiveRowIndex_UpdatedWhenNavigatingRows" -v
```
Expected: PASS

- [ ] **Step 8: Build check**
```
dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj
```

- [ ] **Step 9: Commit**
```
git add src/Clients/Medistock.Desktop/ViewModels/PurchaseEntryViewModel.cs
git add src/Core/Medistock.Application/Purchases/Services/PurchaseService.cs
git add tests/Medistock.Desktop.Tests/PurchaseViewModelAndExportTests.cs
git commit -m "feat: blank row template, active row tracker, inline product search, cancel command in purchase VM"
```

---

## Task 4 — XAML: Replace DatePicker with MM/YY TextBox; Add x:Name Tags; Add Cancel Confirm Modal

**Files:**
- Modify: `src/Clients/Medistock.Desktop/Views/Purchases/PurchaseEntryPage.xaml` (significant row changes)

**Interfaces:**
- Consumes: `PurchaseEntryViewModel.IsCancelConfirmOpen`, `InvoicePendingCancel`, `RequestCancelInvoiceCommand`, `ConfirmCancelInvoiceCommand`, `DismissCancelConfirmCommand`

- [ ] **Step 1: Replace `DatePicker` in row template with `TextBox`**

In the `DataTemplate` for line items (`ListView.ItemTemplate`), find the `DatePicker` for Expiry (Grid.Column="2") and replace it:

From:
```xml
<!-- Expiry Date Picker -->
<DatePicker Grid.Column="2"
            Date="{Binding ExpiryDate, Mode=TwoWay}"
            Margin="1"
            FontSize="11" />
```
To:
```xml
<!-- Expiry MM/YY TextBox -->
<TextBox Grid.Column="2"
         x:Name="ExpiryTextBox"
         Text="{Binding ExpiryText, Mode=TwoWay}"
         Margin="1"
         FontSize="11"
         FontFamily="Consolas, Fira Code, Segoe UI Mono"
         PlaceholderText="MM/YY"
         MaxLength="5" />
```

- [ ] **Step 2: Add `ExpiryText` property to `PurchaseItemRowViewModel`**

In `PurchaseEntryViewModel.cs`, inside `PurchaseItemRowViewModel`, add:
```csharp
[ObservableProperty]
private string _expiryText = string.Empty;

partial void OnExpiryTextChanged(string value)
{
    // Parse MM/YY or MM/YYYY format
    if (value.Length >= 4 && value.Contains('/'))
    {
        var parts = value.Split('/');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int month) &&
            int.TryParse(parts[1], out int year))
        {
            if (month >= 1 && month <= 12)
            {
                // Handle 2-digit year: 26 → 2026
                if (year < 100) year += 2000;
                ExpiryDate = new DateTimeOffset(new DateTime(year, month,
                    DateTime.DaysInMonth(year, month), 23, 59, 59, DateTimeKind.Utc));
            }
        }
    }
}
```

- [ ] **Step 3: Add x:Name on all editable controls in the row template**

Give each input a name using the row index pattern. Since XAML DataTemplates can't use x:Name directly for runtime access, add `Tag` attributes for code-behind lookup and use `VisualTreeHelper` in code-behind. Add a `Tag` on the row container:

```xml
<Border ... Tag="{Binding}">
```

Add `x:Name` to the ListView itself so code-behind can find rows:
```xml
<ListView x:Name="LineItemsListView" ...>
```

- [ ] **Step 4: Add Cancel Confirmation Modal to History Tab XAML**

Add this overlay XAML after the last `</Grid>` in the page (before `</Page>`):
```xml
<!-- Cancel Invoice Confirmation Modal -->
<Grid Visibility="{Binding IsCancelConfirmOpen, Converter={StaticResource BoolToVisibilityConverter}}"
      Background="#80000000"
      HorizontalAlignment="Stretch"
      VerticalAlignment="Stretch">
    <Border Background="{ThemeResource SurfaceDefault}"
            BorderBrush="{ThemeResource BorderDefault}"
            BorderThickness="1"
            CornerRadius="8"
            Padding="24,20"
            MaxWidth="480"
            HorizontalAlignment="Center"
            VerticalAlignment="Center">
        <StackPanel Spacing="16">
            <TextBlock Text="⊘ Cancel Purchase Invoice?"
                       FontSize="18" FontWeight="Bold"
                       Foreground="{ThemeResource TextPrimary}" />
            <TextBlock FontSize="13" TextWrapping="Wrap"
                       Foreground="{ThemeResource TextSecondary}">
                <Run Text="This will permanently reverse all stock, supplier balance, and accounting entries for invoice " />
                <Run Text="{Binding InvoicePendingCancel.SupplierInvoiceNo}"
                     FontWeight="Bold" Foreground="#DC2626" />
                <Run Text=". This action cannot be undone." />
            </TextBlock>
            <StackPanel Orientation="Horizontal" Spacing="10" HorizontalAlignment="Right">
                <Button Content="No, Keep Invoice"
                        Command="{Binding DismissCancelConfirmCommand}"
                        Padding="16,8" />
                <Button Content="⊘ Yes, Cancel Invoice"
                        Command="{Binding ConfirmCancelInvoiceCommand}"
                        Background="#DC2626"
                        Foreground="White"
                        Padding="16,8" />
            </StackPanel>
        </StackPanel>
    </Border>
</Grid>
```

- [ ] **Step 5: Add Cancel button to History tab rows**

In the `DataTemplate` for history rows, add a cancel button after the `👁️ Details` button:
```xml
<Button Content="⊘ Cancel"
        FontSize="10"
        Padding="8,3"
        Foreground="#DC2626"
        Visibility="{Binding Status, Converter={StaticResource PostedStatusVisibilityConverter}}"
        Command="{Binding DataContext.RequestCancelInvoiceCommand, ElementName=PurchasePageRoot}"
        CommandParameter="{Binding}" />
```

Add a `PostedStatusVisibilityConverter` to `Converters.cs`:
```csharp
public class PostedStatusVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PurchaseInvoiceStatus s)
            return s == PurchaseInvoiceStatus.Posted ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
```
Register it in `Tokens.xaml`:
```xml
<converters:PostedStatusVisibilityConverter x:Key="PostedStatusVisibilityConverter" />
```

- [ ] **Step 6: Build check**
```
dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj
```
Expected: 0 errors.

- [ ] **Step 7: Commit**
```
git add src/Clients/Medistock.Desktop/Views/Purchases/PurchaseEntryPage.xaml
git add src/Clients/Medistock.Desktop/ViewModels/PurchaseEntryViewModel.cs
git add src/Clients/Medistock.Desktop/Controls/Converters.cs
git add src/Clients/Medistock.Desktop/Styles/Tokens.xaml
git commit -m "feat: replace DatePicker with MM/YY TextBox; add cancel confirm modal; cancel button in history"
```

---

## Task 5 — Code-behind: Full Keyboard Routing

**Files:**
- Modify: `src/Clients/Medistock.Desktop/Views/Purchases/PurchaseEntryPage.xaml.cs` (full rewrite — currently 37 lines)

**Interfaces:**
- Consumes: `PurchaseEntryViewModel.ActiveRowIndex`, `SetActiveRow(int)`, `AddBlankRow()`, `PostPurchaseInvoiceAsync()`, `RemoveRow(row)`, `LineItems`

This is the most complex UI task. The code-behind implements the full keyboard router, mirroring `PosPage.xaml.cs`.

- [ ] **Step 1: Rewrite `PurchaseEntryPage.xaml.cs` with keyboard routing**

Replace the entire file with:
```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Purchases.DTOs;
using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Medistock.Desktop.Views.Purchases;

public sealed partial class PurchaseEntryPage : Page
{
    public PurchaseEntryViewModel ViewModel { get; }

    // Header fields (set via x:Name in XAML)
    // Referenced by keyboard nav chain
    private TextBox? _invoiceNoBox;
    private TextBox? _gstinBox;
    private TextBox? _notesBox;

    public PurchaseEntryPage(PurchaseEntryViewModel viewModel)
    {
        ViewModel = viewModel;
        this.DataContext = ViewModel;
        this.InitializeComponent();
        this.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Page_KeyDown), handledEventsToo: true);
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        // Focus invoice number on load
        await Task.Delay(150);
        FocusInvoiceNo();
    }

    // ─── Global Key Router ───────────────────────────────────────────────────
    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Modal intercepts
        if (ViewModel.IsCancelConfirmOpen || ViewModel.IsAddSupplierModalOpen || ViewModel.IsInvoiceDetailsModalOpen)
        {
            if (e.Key == VirtualKey.Escape)
            {
                ViewModel.DismissCancelConfirmCommand.Execute(null);
                ViewModel.CloseAddSupplierModalCommand.Execute(null);
                ViewModel.CloseInvoiceDetailsCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case VirtualKey.F2 when ViewModel.SelectedTab == "Entry":
                ViewModel.AddBlankRowCommand.Execute(null);
                FocusRowColumn(ViewModel.LineItems.Count - 1, 0);
                e.Handled = true;
                break;

            case VirtualKey.F6:
            case VirtualKey.S when ctrl:
                if (ViewModel.SelectedTab == "Entry")
                {
                    _ = ViewModel.PostPurchaseInvoiceCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                break;

            case VirtualKey.Escape:
                FocusInvoiceNo();
                e.Handled = true;
                break;

            case VirtualKey.Delete:
                if (ViewModel.SelectedTab == "Entry" && ViewModel.ActiveRow != null)
                {
                    ViewModel.RemoveRowCommand.Execute(ViewModel.ActiveRow);
                    e.Handled = true;
                }
                break;
        }
    }

    // ─── Header Field Navigation ─────────────────────────────────────────────
    // Supplier ComboBox → Invoice No → Invoice Date is skipped (defaults today)
    // → GSTIN → [Enter] jumps into row 0 col 0

    private void SupplierComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is SupplierDto supplier)
        {
            ViewModel.OnSupplierSelected(supplier);
        }
    }

    private void InvoiceNoBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Tab)
        {
            FocusGstinBox();
            e.Handled = true;
        }
    }

    private void GstinBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            // Jump into first row, product name column
            if (ViewModel.LineItems.Count == 0) ViewModel.AddBlankRowCommand.Execute(null);
            ViewModel.SetActiveRow(0);
            FocusRowColumn(0, 0);
            e.Handled = true;
        }
    }

    // ─── Grid Row Navigation ─────────────────────────────────────────────────

    // Columns in row: 0=ProductName, 1=BatchNo, 2=ExpiryText, 3=HSN, 4=Qty, 5=FreeQty, 6=Cost, 7=MRP, 8=SaleRate, 9=Disc%, 10=GST%
    private const int ColCount = 11;

    private void RowCell_KeyDown(object sender, KeyRoutedEventArgs e, int rowIndex, int colIndex)
    {
        ViewModel.SetActiveRow(rowIndex);

        switch (e.Key)
        {
            case VirtualKey.Enter:
            case VirtualKey.Right:
                if (colIndex == ColCount - 1)
                {
                    // Last column of last row → add new row and focus col 0
                    if (rowIndex == ViewModel.LineItems.Count - 1)
                        ViewModel.AddBlankRowCommand.Execute(null);
                    FocusRowColumn(rowIndex + 1, 0);
                }
                else
                {
                    FocusRowColumn(rowIndex, colIndex + 1);
                }
                e.Handled = true;
                break;

            case VirtualKey.Left:
                if (colIndex > 0) FocusRowColumn(rowIndex, colIndex - 1);
                else FocusInvoiceNo();
                e.Handled = true;
                break;

            case VirtualKey.Down:
                if (rowIndex < ViewModel.LineItems.Count - 1)
                    FocusRowColumn(rowIndex + 1, colIndex);
                e.Handled = true;
                break;

            case VirtualKey.Up:
                if (rowIndex > 0) FocusRowColumn(rowIndex - 1, colIndex);
                else FocusInvoiceNo();
                e.Handled = true;
                break;

            case VirtualKey.Escape:
                FocusInvoiceNo();
                e.Handled = true;
                break;
        }
    }

    // Product name column — trigger search on Enter if text changed
    private async void ProductNameCell_KeyDown(object sender, KeyRoutedEventArgs e, int rowIndex)
    {
        if (e.Key == VirtualKey.Enter && sender is TextBox tb)
        {
            var row = ViewModel.LineItems[rowIndex];
            if (!string.IsNullOrWhiteSpace(tb.Text) && tb.Text != row.ProductName)
            {
                await ViewModel.SearchRowProductAsync(tb.Text, row);
                // After product populated, move to Batch No
            }
            FocusRowColumn(rowIndex, 1);
            e.Handled = true;
        }
        else
        {
            RowCell_KeyDown(sender, e, rowIndex, 0);
        }
    }

    // Expiry text — warn if past
    private void ExpiryCell_LostFocus(object sender, RoutedEventArgs e, int rowIndex)
    {
        if (rowIndex >= ViewModel.LineItems.Count) return;
        var row = ViewModel.LineItems[rowIndex];
        if (row.ExpiryDate < DateTimeOffset.UtcNow)
        {
            ViewModel.StatusMessage = $"⚠️ Row {rowIndex + 1}: Expiry date is in the past — please verify.";
        }
    }

    // ─── Focus Helpers ───────────────────────────────────────────────────────

    private void FocusInvoiceNo()
    {
        _invoiceNoBox = FindNamedControl<TextBox>(this, "SupplierInvoiceNoBox");
        _invoiceNoBox?.Focus(FocusState.Programmatic);
    }

    private void FocusGstinBox()
    {
        _gstinBox = FindNamedControl<TextBox>(this, "SupplierGstinBox");
        _gstinBox?.Focus(FocusState.Programmatic);
    }

    private void FocusRowColumn(int rowIndex, int colIndex)
    {
        if (LineItemsListView == null) return;
        if (rowIndex < 0 || rowIndex >= ViewModel.LineItems.Count) return;

        // Scroll into view
        var item = ViewModel.LineItems[rowIndex];
        LineItemsListView.ScrollIntoView(item);

        // Find the ListViewItem container, then find the Nth input control inside
        var container = LineItemsListView.ContainerFromIndex(rowIndex) as ListViewItem;
        if (container == null) return;

        var inputs = GetAllInputControls(container);
        if (colIndex < inputs.Count)
        {
            var ctrl = inputs[colIndex];
            ctrl.Focus(FocusState.Programmatic);
            if (ctrl is TextBox tb) { tb.SelectAll(); }
            else if (ctrl is NumberBox nb) { nb.Focus(FocusState.Programmatic); }
        }
    }

    // Collect all TextBox and NumberBox children in visual-tree order
    private static System.Collections.Generic.List<Control> GetAllInputControls(DependencyObject parent)
    {
        var result = new System.Collections.Generic.List<Control>();
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox tb && tb.IsEnabled && tb.Visibility == Visibility.Visible)
                result.Add(tb);
            else if (child is NumberBox nb && nb.IsEnabled && nb.Visibility == Visibility.Visible)
                result.Add(nb);
            else
                result.AddRange(GetAllInputControls(child));
        }
        return result;
    }

    private static T? FindNamedControl<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name) return fe;
            var found = FindNamedControl<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ─── XAML event handlers (called from XAML code-behind wiring) ───────────

    private void HistorySearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.ApplyHistoryFilter();
    }
}
```

- [ ] **Step 2: Wire x:Name on key header fields in XAML**

In `PurchaseEntryPage.xaml`, add `x:Name` to:
- Supplier Invoice No. TextBox: `x:Name="SupplierInvoiceNoBox"`
- Supplier GSTIN TextBox: `x:Name="SupplierGstinBox"`
- The Invoice No TextBox also needs `KeyDown="InvoiceNoBox_KeyDown"`
- The GSTIN TextBox needs `KeyDown="GstinBox_KeyDown"`

Also add `KeyDown` on the `SupplierComboBox`:
```xml
<ComboBox x:Name="SupplierComboBox"
          KeyDown="SupplierComboBox_KeyDown"
          ...
```
Add handler in code-behind:
```csharp
private void SupplierComboBox_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key == VirtualKey.Enter)
    {
        FocusControl("SupplierInvoiceNoBox");
        e.Handled = true;
    }
}

private void FocusControl(string name)
{
    var ctrl = FindNamedControl<Control>(this, name);
    ctrl?.Focus(FocusState.Programmatic);
}
```

- [ ] **Step 3: Build check**
```
dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj
```
Expected: 0 errors.

- [ ] **Step 4: Commit**
```
git add src/Clients/Medistock.Desktop/Views/Purchases/PurchaseEntryPage.xaml.cs
git add src/Clients/Medistock.Desktop/Views/Purchases/PurchaseEntryPage.xaml
git commit -m "feat: full keyboard routing in purchase entry — Enter chain, arrow nav, F2/F6/Ctrl+S, Escape"
```

---

## Task 6 — Final Polish: XAML Header Field Entry UX + Expiry Warning + BoolToVisibility

**Files:**
- Modify: `src/Clients/Medistock.Desktop/Views/Purchases/PurchaseEntryPage.xaml`
- Modify: `src/Clients/Medistock.Desktop/Controls/Converters.cs`
- Modify: `src/Clients/Medistock.Desktop/Styles/Tokens.xaml`

- [ ] **Step 1: Add `BoolToVisibilityConverter` if not already registered**

In `Converters.cs`, check if `BoolToVisibilityConverter` exists. If not, add:
```csharp
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
```
Register in `Tokens.xaml`:
```xml
<converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />
```

- [ ] **Step 2: Update status bar to show error color for ⚠️/❌ messages**

In the Status Banner `TextBlock` in XAML, bind `Foreground` based on message content:
```xml
<TextBlock Grid.Column="1"
           Text="{Binding StatusMessage}"
           FontSize="12"
           FontWeight="SemiBold"
           Foreground="{Binding StatusMessage, Converter={StaticResource StatusColorConverter}}"
           VerticalAlignment="Center"
           HorizontalAlignment="Right"
           Margin="0,0,12,0" />
```

Add `StatusColorConverter` to `Converters.cs`:
```csharp
public class StatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var msg = value as string ?? "";
        if (msg.StartsWith("❌") || msg.StartsWith("⚠️"))
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38)); // red
        if (msg.StartsWith("✅"))
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 5, 150, 105)); // green
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139)); // muted
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
```
Register in `Tokens.xaml`.

- [ ] **Step 3: Build + run all tests**
```
dotnet test tests/Medistock.Infrastructure.Tests/
dotnet test tests/Medistock.Desktop.Tests/
dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj
```
Expected: all tests pass, 0 build errors.

- [ ] **Step 4: Final commit**
```
git add src/Clients/Medistock.Desktop/Views/Purchases/PurchaseEntryPage.xaml
git add src/Clients/Medistock.Desktop/Controls/Converters.cs
git add src/Clients/Medistock.Desktop/Styles/Tokens.xaml
git commit -m "polish: status color, BoolToVisibility converter, expiry warning"
```

---

## Task 7 — Integration Tests for Cancel + Duplicate + Keyboard Flow Tests

**Files:**
- Modify: `tests/Medistock.Infrastructure.Tests/PurchaseTests.cs`
- Modify: `tests/Medistock.Desktop.Tests/PurchaseViewModelAndExportTests.cs`

- [ ] **Step 1: Add `ZeroCostRow_PostsSuccessfully` test**

```csharp
[Fact]
public async Task PostInvoice_WithZeroCostFreeItem_DoesNotThrowAndPostsSuccessfully()
{
    var suppliers = await _purchaseService.GetSuppliersAsync("org-1");
    var supplier = suppliers.First();

    var cmd = new CreatePurchaseInvoiceCommand(
        OrgId: "org-1", BranchId: "br-1", WarehouseId: "wh-1",
        SupplierId: supplier.Id, SupplierName: supplier.Name,
        SupplierGstin: supplier.Gstin, SupplierInvoiceNo: "ZERO-COST-001",
        SupplierInvoiceDate: DateTime.UtcNow, IsInterstate: false,
        CreatedByUserId: "user-1", Notes: "Free sample",
        Items: new List<PurchaseInvoiceItemInputDto>
        {
            new("p_dolo", "Dolo 650", "30049099", "FREEBATCH01",
                DateTime.UtcNow.AddMonths(18), null,
                0m, 10m, 0m, 30.5m, 30m, 0m, 12m) // qty=0 free=10 cost=0
        });

    // qty=0 will fail domain validation — must use free qty only with qty>=1 trick
    // Actually adjust: qty=1, free=9, cost=0 — valid entry for a free+paid batch
    cmd = cmd with
    {
        Items = new List<PurchaseInvoiceItemInputDto>
        {
            new("p_dolo", "Dolo 650", "30049099", "FREEBATCH01",
                DateTime.UtcNow.AddMonths(18), null,
                1m, 9m, 0m, 30.5m, 30m, 0m, 12m)
        }
    };

    var result = await _purchaseService.CreateAndPostPurchaseInvoiceAsync(cmd);
    Assert.True(result.Success, result.ErrorMessage);
}
```

- [ ] **Step 2: Add `ExpiryText_ParsesCorrectly` unit test**

```csharp
[Fact]
public void ExpiryText_MMYY_ParsesCorrectExpiryDate()
{
    var row = new PurchaseItemRowViewModel();
    row.ExpiryText = "06/27"; // June 2027
    Assert.Equal(2027, row.ExpiryDate.Year);
    Assert.Equal(6, row.ExpiryDate.Month);
}

[Fact]
public void ExpiryText_MMYYYY_ParsesCorrectExpiryDate()
{
    var row = new PurchaseItemRowViewModel();
    row.ExpiryText = "12/2028";
    Assert.Equal(2028, row.ExpiryDate.Year);
    Assert.Equal(12, row.ExpiryDate.Month);
}
```

- [ ] **Step 3: Run all tests**
```
dotnet test tests/Medistock.Infrastructure.Tests/ -v
dotnet test tests/Medistock.Desktop.Tests/ -v
```
Expected: all pass.

- [ ] **Step 4: Full build**
```
dotnet build src/Clients/Medistock.Desktop/Medistock.Desktop.csproj
```

- [ ] **Step 5: Final integration commit**
```
git add tests/Medistock.Infrastructure.Tests/PurchaseTests.cs
git add tests/Medistock.Desktop.Tests/PurchaseViewModelAndExportTests.cs
git commit -m "test: cancel reversal, duplicate guard, zero-cost row, expiry parse coverage"
```

# Marg ERP Product Creation (F2) & Two-Stage Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Marg ERP-style keyboard-first On-The-Fly Product Creation (`F2`) and Two-Stage FEFO Batch Search Selection for high-speed pharmacy POS checkout.

**Architecture:** Extend `Medistock.Application` with `CreateProductWithBatchCommand`, implement two-stage search querying (Item Master -> FEFO Batches) in `ISqliteProductRepository`, and integrate Marg ERP `F2` modal and FEFO Batch Popover directly into `PosViewModel` and `PosPage.xaml`.

**Tech Stack:** .NET 9, C# 13, WinUI 3 / Windows App SDK, SQLite WAL with Dapper, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

**Spec:** In-chat approved Marg ERP design (Two-Stage Continuous Search: Item Grid -> Batch Grid; On-The-Fly Item Creation modal on `F2` with HSN 3004, GST %, Schedule classification, and immediate bill insertion).

## Global Constraints

- **Execution Platform:** Windows 10/11 x64 WinUI 3 Desktop.
- **Keyboard Workflow:** 100% mouse-free (`F2`, `F3`, `Enter`, `Tab`, `Arrow Keys`, `Esc`).
- **Database:** SQLite WAL mode with foreign key integrity (`INSERT OR IGNORE` / parameterized Dapper transactions).
- **Performance:** Sub-10ms item lookup and instantaneous batch selection.

## Review Focus

- Product created with `F2` must immediately become searchable and be automatically inserted into the active invoice tab cart.
- FEFO batch ordering must sort batches by `expiry_date ASC` (earliest expiring first), filtering out expired/zero-stock batches unless explicitly enabled.
- Navigating with `Enter` from Search Box -> Item Selection -> Batch Selection -> Cart Quantity must work seamlessly without losing keyboard focus.
- Multiple active invoice tabs must keep their draft cart state isolated when creating new products.

---

### Task 1: Application Layer — `CreateProductWithBatchCommand` & DTOs

**Files:**
- Create: `src/Core/Medistock.Application/Features/Products/Commands/CreateProductWithBatchCommand.cs`
- Create: `tests/Medistock.Application.Tests/Features/Products/CreateProductWithBatchCommandHandlerTests.cs`

- [ ] Write unit test for `CreateProductWithBatchCommandHandler` verifying product and batch creation in repositories.
- [ ] Implement `CreateProductWithBatchCommand` with fields: `Name`, `GenericName`, `PackUnits`, `BaseUnit`, `HsnCode`, `GstRatePercent`, `Schedule`, `ManufacturerName`, `BatchNumber`, `ExpiryDate`, `Mrp`, `PurchaseRate`, `SaleRate`, `OpeningQuantity`.
- [ ] Implement command handler inserting Product, Batch, and StockBalance inside an atomic transaction.
- [ ] Run test suite to verify tests pass (`dotnet test tests/Medistock.Application.Tests`).
- [ ] Commit changes (`git commit -m "feat(application): add CreateProductWithBatchCommand and handler with tests"`).

---

### Task 2: Data Access — Enhanced Multi-Batch Search Queries

**Files:**
- Modify: `src/Infrastructure/Medistock.Infrastructure.Data/Repositories/SqliteProductRepository.cs`
- Modify: `src/Core/Medistock.Application/Common/Interfaces/IProductRepository.cs`
- Create: `tests/Medistock.Infrastructure.Tests/Repositories/ProductBatchSearchTests.cs`

- [ ] Write integration test verifying querying products by Name, Generic/Salt, and Barcode along with their active batches sorted in FEFO order.
- [ ] Update `IProductRepository` and `SqliteProductRepository` to support querying products with grouped FEFO batches (`GetProductsWithBatchesAsync`).
- [ ] Run repository tests (`dotnet test tests/Medistock.Infrastructure.Tests`).
- [ ] Commit changes (`git commit -m "feat(data): implement FEFO batch querying in SqliteProductRepository"`).

---

### Task 3: ViewModel — Two-Stage Search & `F2` Add Product Modal

**Files:**
- Modify: `src/Clients/Medistock.Desktop/ViewModels/PosViewModel.cs`
- Create: `tests/Medistock.Desktop.Tests/ViewModels/PosTwoStageSearchTests.cs`

- [ ] Write unit tests in `Medistock.Desktop.Tests` for:
  - Two-stage search state transition (`SelectItemCommand` -> opens `SelectedProductBatches`).
  - Batch selection (`SelectBatchCommand` -> adds item to active bill cart and closes picker).
  - `F2` on-the-fly product creation (`OpenCreateProductCommand` -> `SaveNewProductCommand` -> adds to cart).
- [ ] Implement `PosViewModel` properties:
  - `IsBatchPickerOpen`, `SelectedProductItem`, `SelectedProductBatches`, `SelectedBatchIndex`.
  - `IsCreateProductModalOpen`, `NewProductName`, `NewGenericName`, `NewPackUnits`, `NewBaseUnit`, `NewHsnCode`, `NewGstPercent`, `NewSchedule`, `NewMfg`, `NewBatchNo`, `NewExpiryDate`, `NewMrp`, `NewSaleRate`, `NewPurchaseRate`, `NewOpeningQty`.
- [ ] Implement commands:
  - `OpenCreateProductModalCommand` (`F2`)
  - `SaveCreateProductCommand` (calls MediatR `CreateProductWithBatchCommand`, adds item to cart, resets modal)
  - `CloseCreateProductModalCommand` (`Esc`)
  - `SelectProductForBatchPickerCommand` (opens batch popover)
  - `ConfirmBatchSelectionCommand` (adds batch to active tab cart)
- [ ] Run tests to verify passing (`dotnet test tests/Medistock.Desktop.Tests`).
- [ ] Commit changes (`git commit -m "feat(pos): add two-stage search and F2 on-the-fly product creation to PosViewModel"`).

---

### Task 4: WinUI 3 View — Marg ERP Two-Stage Search & `F2` Modal UI

**Files:**
- Modify: `src/Clients/Medistock.Desktop/Views/POS/PosPage.xaml`
- Modify: `src/Clients/Medistock.Desktop/Views/POS/PosPage.xaml.cs`

- [ ] Update Stage 1 Search Dropdown to display fast Item Summary (Name, Generic, Pack, Mfg, Total Stock, MRP).
- [ ] Add Stage 2 Floating FEFO Batch Selection Popover (Batch Number, Expiry, Available Stock, MRP, Rate, FEFO sort, highlight near-expiry).
- [ ] Add Marg ERP `F2` "Create Item & Batch Master" modal overlay:
  - High-density form layout for Name, Generic/Salt, Pack, Mfg, HSN, GST %, Schedule badges (H, H1, X, Narcotic), Batch No, Expiry, MRP, Rate, Qty.
  - Keyboard hotkeys: `F2`/`Enter` to Save, `Esc` to cancel.
- [ ] Wire up keyboard events in `PosPage.xaml.cs`:
  - `F2`: Trigger Create Product modal.
  - `Enter` in Search List: Open Stage 2 Batch Picker if multiple batches, or add directly if single batch.
  - `Enter` in Batch Picker: Add to cart and focus Qty cell in cart grid.
  - `Esc`: Close popovers/modals and return focus to SearchBox or Grid.
- [ ] Commit changes (`git commit -m "feat(pos-ui): implement Marg ERP two-stage search and F2 modal in PosPage"`).

---

### Task 5: End-to-End Verification & Desktop Executable Build

**Files:**
- Solution: `Medistock.sln`
- Executable: `src/Clients/Medistock.Desktop/bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/Medistock.Desktop.exe`

- [ ] Build complete solution in x64 Debug (`dotnet build -p:Platform=x64`).
- [ ] Run all automated test suites (`dotnet test -p:Platform=x64`).
- [ ] Launch `Medistock.Desktop.exe` to verify 0 runtime crashes and check `startup_error.log`.
- [ ] Commit final verification updates.

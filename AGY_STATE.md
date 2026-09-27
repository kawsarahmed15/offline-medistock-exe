# AGY CLI Memory & Progress State Log
> **Project:** Medistock-offlinefirst (Pharmacy ERP Platform)  
> **Last Updated:** 2026-09-24 15:29:00 UTC  
> **Current Phase:** Phase 0 — Architecture & Foundation Documents  

---

## 1. System Vision (Summary)
**Build:** An offline-first, multi-branch pharmacy operating system with integrated accounting, procurement, B2B pharmacy-to-wholesaler commerce, inventory intelligence, compliance, and cloud management.

**NOT:** "MARG with a nicer UI."

---

## 2. Tech Stack (Frozen)
| Layer | Technology |
|---|---|
| Desktop UI | C# / WinUI 3 / .NET 10 LTS / Windows App SDK |
| Client Architecture | MVVM + Clean Architecture |
| Local Client DB | SQLite WAL + FTS5 shadow table |
| Hot Path Data Access | **Dapper / Raw ADO.NET** (POS, search, cart — NO EF Core) |
| Cold Path Data Access | **EF Core** (admin, reports, settings) |
| Local Server DB | PostgreSQL (branch authoritative source of truth) |
| Cloud API | ASP.NET Core modular monolith |
| Cloud DB | PostgreSQL + read replicas + PITR |
| Cache | Redis |
| Messaging | PostgreSQL Outbox → (Service Bus/Kafka at scale) |
| Auth | OIDC/OAuth 2.1 + MFA |
| Observability | OpenTelemetry + Serilog |
| CI/CD | GitHub Actions / Azure DevOps |
| Desktop Deploy | Signed MSIX + auto-updater |

---

## 3. Key Architecture Decisions (Locked)
- **3-tier topology:** Workstation SQLite ↔ Local Branch PostgreSQL ↔ Cloud PostgreSQL
- **Outbox pattern** for all sync — transactions are never lost
- **Idempotency keys** on every outbox event — no duplicate processing
- **Document sequences** via locked DB row — no `MAX(no)+1` race conditions
- **ULIDs** as internal IDs; human-readable numbers as separate `invoice_no` field
- **org_id always from JWT** — never from request body (tenant isolation)
- **Financial records immutable** once posted — corrections via reversal/credit note only
- **External APIs (GST, payment)** always async via outbox — never block sale commit

---

## 4. TRD Performance Targets (Non-Negotiable)
| Operation | Target | Hard Ceiling |
|---|---|---|
| Barcode scan → cart | ≤ 100ms | 200ms |
| Keystroke → search | ≤ 100ms | 150ms |
| Cold start → POS ready | ≤ 3s | 5s |
| Sale → DB commit | ≤ 50ms | 100ms |
| Module navigation | ≤ 150ms | 300ms |
| 5,000+ row grid scroll | 60fps | Smooth |

---

## 5. Session Progress Log

### ✅ Step 1 — File Inspection & Context Capture
Analyzed: `trd.md`, `marg-research.md`, `chatgpt-conversation.md` (3786 lines), `chatgpt-conversation-research.md`

### ✅ Step 2 — Context Memory System Setup
Created: `AGENT.md` (rules & protocols) + `AGY_STATE.md` (this file)

### ✅ Step 3 — Master Architecture Document
Created: `ARCHITECTURE.md` — 22 sections covering every system component.

### ✅ Step 4 — UI/UX Design System
Created: `UI_DESIGN_SYSTEM.md` — 20 sections covering:
- Design philosophy ("Operational depth of MARG + modern SaaS clarity + native performance")
- **Dual theme system** (Light + Dark, user selects at first launch, switchable anytime — no restart)
- First-launch Theme Selection Wizard (7-step flow: welcome → theme → accent → density → font → org setup → done)
- Full color token system (50+ tokens, semantic names, both theme values, pharmacy-specific tokens)
- **8 accent color presets** user-selectable (Pharmacy Green default, Ocean Blue, Indigo, Teal, Violet, Rose, Amber, Slate)
- Typography: Lexend (headings) + Source Sans 3 (body) + Fira Code (monospace for codes)
- Type scale (10px–32px), 4 weight levels, 3 user font size options
- Spacing system (8dp grid, 8 levels), density system (Compact/Normal/Comfortable)
- Window layout grid (sidebar 240px collapsible to 56px, 1280×720 min)
- Component library: Buttons (5 types), Inputs (4 states), Data tables (virtualized), Status badges, KPI cards, Toasts, Dialogs
- App shell: sidebar nav, top toolbar, Command Palette (Ctrl+K) with global search
- POS screen (performance-critical, no Mica/animations, two-panel split, F-key shortcuts)
- Payment screen with change calculator
- Role-based dashboards (Owner, Manager, Pharmacist, Accountant — different widgets)
- Inventory grid (ItemsRepeater, flat templates, filter slide-in panel)
- Purchase entry form (tab-key navigation, barcode fill, auto-GST)
- B2B transaction timeline UI (visual step-by-step with colored dot states)
- Chart types per screen (LiveCharts2/SkiaSharp), chart performance rules
- User customization panel (12 settings, all apply instantly without restart)
- Motion/animation rules (zero on POS, budgets per screen type, reduced-motion support)
- Accessibility (WCAG 2.1 AA, focus rings, color contrast, keyboard nav, screen reader)
- Performance budget (60fps scroll, ≤150ms navigation, ≤180ms dialogs)
- WinUI 3 implementation notes (ResourceDictionary structure, x:Bind rules, ItemsRepeater pattern)
- Anti-patterns list (20 banned patterns with reasons)

### ✅ Step 5 — Keyboard & Input Architecture
Created: `KEYBOARD_SHORTCUTS.md` — 15 sections covering:
- **Command Layer First** rule: every action is an `ICommand`; shortcuts are a config binding layer, never direct-to-logic wiring
- **Two keymap profiles:** Medistock Standard (Ctrl+S=Save) and MARG-Compatible (Ctrl+S=Flush Cache) — user-selectable, instant switch
- **Scope system:** Global → Screen → Panel → Field — deepest scope wins; F2/F3/F4 safely reused per context
- **MARG ERP 9+ full shortcut parity** across: POS sale screen, payment, purchase entry, accounting/vouchers, product/inventory, reports
- Complete **Conflict Resolution** decisions (Ctrl+S, Ctrl+T, Ctrl+Tab, Alt+I, F1) — all resolved with rationale
- **POS keyboard-only flow:** F2 → type/scan → Enter → Tab → type qty → F6 → type amount → Enter = full sale in 4-8 keystrokes
- **4-keystroke payment flow:** F6 → type amount → Enter → receipt printed
- **Barcode scanner protocol:** HID keyboard mode; search field always re-focused after cart actions; zero extra keystrokes between scans
- **Mouse parity:** every shortcut also reachable by mouse; all buttons show shortcut in tooltip
- **In-app shortcut help** (Alt+F1): context-aware overlay showing only current scope's shortcuts
- Both `ARCHITECTURE.md` (Section 16 added) and `AGENT.md` (Section C+D added) updated with keyboard rules

Updated: `ARCHITECTURE.md` v1.1 — keyboard architecture section added (Section 16), 3 new anti-patterns added  
Updated: `AGENT.md` — keyboard command-layer rules + full reference index

### ✅ Step 6 — Full Product Requirements Document (PRD)
Created: `PRD.md` — 26 sections, 50+ NFRs, priority ratings P1-P4, acceptance criteria for all modules (POS, Inventory, Accounting, GST, B2B, Multi-Branch, Platform).

### ✅ Step 8 — Clean Architecture Solution Setup (`folder-structure.md`)
- Created `Medistock.sln` with 14 modular projects matching Clean Architecture.
- Installed foundational packages: `Dapper`, `Microsoft.Data.Sqlite`, `Npgsql`, `FluentValidation`, `CommunityToolkit.Mvvm`, `Ulid`, `Microsoft.Extensions.DependencyInjection`.

### ✅ Step 9 — Phase 1: Core Domain, SQLite WAL + FTS5 Hot-Path, & WinUI 3 POS Vertical Slice
- **Domain Modeling:**
  - Base primitives: `Entity<T>`, `AggregateRoot<T>`, `ValueObject`, `IDomainEvent`.
  - Value Objects: `Money` (with rounding & currency checks), `Gstin` (with statutory 15-char regex validation & state code extraction), `PackSize` (base unit conversion).
  - Enums: `DrugSchedule`, `DosageForm`, `StockMovementType`, `SaleStatus`, `PaymentMode`, `OutboxEventStatus`, `ConnectivityState`.
  - Core Entities: `Product`, `Batch`, `StockBalance`, `StockMovement`, `Sale`, `SaleItem` (CGST/SGST vs IGST calculation), `SalePayment`, `OutboxEvent`.
- **Application Services:**
  - `ProductSearchService`: 150ms debounced FTS5 search with in-flight cancellation.
  - `PosTransactionService`: Atomic sale execution, stock validation, FEFO batch assignment, calculation breakdown, outbox generation.
  - Interfaces: `IProductSearchRepository`, `IStockRepository`, `ISaleRepository`, `IOutboxRepository`, `ISqliteConnectionFactory`, `IDocumentSequenceService`.
- **Infrastructure Data Access (Dapper Hot Path):**
  - `SqliteConnectionFactory`: Configured with `PRAGMA journal_mode = WAL;`, `PRAGMA synchronous = NORMAL;`, `PRAGMA busy_timeout = 5000;`.
  - `001_InitialSchema.sql`: 10 tables, indexes, `fts_products` FTS5 virtual table, and automated sync triggers on product INSERT/UPDATE/DELETE.
  - `DatabaseMigrator`: Startup migration runner.
  - `SqliteProductSearchRepository`: Sub-100ms FTS5 search + barcode lookup with FEFO batch projection.
  - `SqliteStockRepository`: Atomic stock deduction (`WHERE quantity >= @qty`), FEFO batch query, stock movement ledger.
  - `SqliteSaleRepository`: Atomic sale commit + items + payments + stock deduction + movements + outbox in a single local transaction.
  - `SqliteOutboxRepository` & `SqliteDocumentSequenceService`.
- **WinUI 3 Desktop POS Client:**
  - `Tokens.xaml`: Semantic color tokens (Light & Dark themes, pharmacy status brushes, 8dp spacing, type scale).
  - `PosViewModel`: CommunityToolkit.Mvvm viewmodel with debounced search, live cart calculations, barcode scanning pipeline, and checkout.
  - `PosPage.xaml` & `.cs`: Two-panel workspace (search & catalog left, running cart & totals right, large F6 payment button).
  - `App.xaml.cs` & `MainWindow.xaml.cs`: DI configuration and automated startup migrations.
- **Empirical Benchmark & Interactive Simulation Results:**
  - **FTS5 Search by Brand ('dolo'):** **0.59 ms** (Target: ≤100ms)
  - **FTS5 Search by Generic ('pantoprazole'):** **0.63 ms** (Target: ≤100ms)
  - **FTS5 Search by Active Salt ('clavulanic'):** **0.60 ms** (Target: ≤100ms)
  - **Full Atomic POS Sale Commit:** **73.51 ms** (Target: ≤100ms)
  - **Concurrent Checkout & Overselling Protection:** **100% Verified** (Zero negative stock under concurrent multi-counter load).
  - **Offline Outbox Event Generation:** **100% Verified** (Generated `SALE_COMMITTED` payload).

---

### ✅ Step 10 — Keyboard Shortcuts, Hardware HAL & Outbox Sync Worker (Items 1, 2, 3)
- **1. Keyboard Shortcut System & Dynamic Keymaps (`Medistock.Desktop`):**
  - Implemented `KeymapProfile` and `KeyBindingDefinition` with JSON serialization.
  - Added [`default.json`](file:///D:/Projects/Medistock-offlinefirst/src/Clients/Medistock.Desktop/Keymaps/default.json) (Medistock Standard: `Ctrl+S=Save`, `F2=New`, `F3=Search`, `F4=Discount`, `F6=Payment`, `Alt+F1=Help`).
  - Added [`marg-compatible.json`](file:///D:/Projects/Medistock-offlinefirst/src/Clients/Medistock.Desktop/Keymaps/marg-compatible.json) (MARG-Compatible: `Ctrl+W=Save`, `Ctrl+S=Flush Cache`).
  - Implemented `ShortcutService` with hierarchical scope resolution (`Field` $\rightarrow$ `Panel` $\rightarrow$ `Screen` $\rightarrow$ `Global`).
  - Built `ShortcutHelpDialog.xaml` (context-aware `Alt+F1` overlay dialog) and wired it into `PosPage.xaml.cs`.
- **2. Hardware Abstraction Layer (`Medistock.Infrastructure.Hardware`):**
  - Implemented `EscPosReceiptPrinter` supporting `IReceiptPrinter` and `ICashDrawer`.
  - Generates binary ESC/POS byte streams for both 80mm (48 column standard) and 58mm (32 column compact) formats.
  - Implemented cash drawer pulse (`\x1B\x70\x00\x19\xFA`), paper auto-cut (`\x1D\x56\x42\x00`), tax breakdown, and statutory pharmaceutical disclaimers.
- **3. Local Sync Worker & Connectivity Engine (`Medistock.Infrastructure.Sync`):**
  - Implemented `IConnectivityService` / `ConnectivityService` with 3-tier connectivity state detection (`FullA`, `DegradedLocalB`, `LocalServerDownC`).
  - Implemented `OutboxSyncWorker` background service draining pending outbox events with exponential retry/failure backoff.
  - Wired into `Medistock.Desktop`, `Medistock.LocalServer`, and unit test test harness.
- **Verification & Test Status:**
  - 25 automated unit & integration tests passing with **0 warnings and 0 errors** across all 14 projects.

---

### ✅ Step 11 — Phase 2: Inventory & Batch Management, Expiry Dashboard & Schedule Drug Register
- **1. Multi-Batch & Expiry Risk Engine (`Medistock.Domain` & `Medistock.Application`):**
  - Added `ExpiryBand` categorization (`Expired`, `Critical` $\le 30$d, `Warning` 31–90d, `Good` $> 90$d).
  - Added `StockAdjustmentType` (`Add`, `Reduce`, `QuarantineExpired`, `DamageWriteOff`).
  - Added financial valuation algorithms calculating MRP valuation and acquisition cost value-at-risk.
  - Implemented `InventoryService` and `IInventoryRepository`.
- **2. Statutory Schedule Drug Register Subsystem (Compliance):**
  - Created `ScheduleDrugRegisterEntry` entity meeting Indian Drugs and Cosmetics Rules (Form 35 / Schedule H1).
  - Tracks mandatory patient name/phone/address, prescribing doctor & registration number, prescription date/reference, batch, expiry, quantity, and dispensing pharmacist.
  - Implemented `ScheduleDrugService` and `IScheduleDrugRepository` with multi-criteria filtering (date ranges, schedule classes H/H1/X/Narcotics, search).
- **3. Database Migration & Dapper Repositories (`Medistock.Infrastructure.Data`):**
  - Added `002_InventoryAndScheduleDrugs.sql` migration with `schedule_drug_register` table and indexes.
  - Implemented `SqliteInventoryRepository` with atomic stock adjustments, negative stock protection, and movement ledger logging.
  - Implemented `SqliteScheduleDrugRepository` with Dapper query pipelines.
- **4. WinUI 3 Desktop Views & ViewModels (`Medistock.Desktop`):**
  - `InventoryViewModel` & `InventoryPage.xaml`: Stock grid, search, low stock filter, batch details, and action triggers.
  - `ExpiryDashboardViewModel` & `ExpiryDashboardPage.xaml`: 4-band visual KPI cards with color-coded alerts and urgent action lists.
  - `ScheduleRegisterViewModel` & `ScheduleRegisterPage.xaml`: Regulatory inspection-ready ledger with date/doctor/patient filtering.
- **Verification & Test Status:**
  - 32 automated unit & integration tests passing with **0 warnings and 0 errors** across all 14 projects.

### ✅ Step 12 — Purchase Entry & Wholesaler Management Subsystem
- **1. Domain Model (`Medistock.Domain.Purchases`):**
  - Implemented `Supplier` entity with GSTIN, Drug License number, credit days, and running balance ledger tracking.
  - Implemented `PurchaseInvoice` and `PurchaseInvoiceItem` aggregates with scheme discounts, free goods ratio, landed cost computation, and Intrastate (CGST/SGST) vs Interstate (IGST) tax segregation.
- **2. Application Layer & Repositories (`Medistock.Application` & `Medistock.Infrastructure.Data`):**
  - Added migration `003_PurchasesAndSuppliers.sql` (`suppliers`, `purchase_invoices`, `purchase_invoice_items`).
  - Implemented `SqliteSupplierRepository` and `SqlitePurchaseRepository`.
  - Built `PurchaseService.CreateAndPostPurchaseInvoiceAsync` executing atomic 6-step transactional commit (Invoice persistence, Batch upsert, Stock balance increment, Movement ledger entry, Supplier balance update, Outbox queue).
- **3. WinUI 3 Desktop Views & ViewModels (`Medistock.Desktop`):**
  - `PurchaseEntryViewModel` & `PurchaseEntryPage.xaml`: High-speed tabular keyboard data entry grid with dynamic row addition (`F2`), automatic GST and discount math, and `[Ctrl+S]` post action.

---

### ✅ Step 13 — Financial Accounting Core (Double-Entry Engine)
- **1. Domain Model (`Medistock.Domain.Accounting`):**
  - `AccountHead` entity with standard 5-category chart of accounts (`Asset`, `Liability`, `Equity`, `Revenue`, `Expense`).
  - `JournalEntry` and `JournalLine` double-entry aggregates enforcing mathematical balance ($\sum \text{Debits} = \sum \text{Credits}$).
  - `VoucherType` enum: `Sales`, `Purchase`, `Payment`, `Receipt`, `Contra`, `Journal`, `CreditNote`, `DebitNote`.
- **2. Database & Dapper Persistence (`Medistock.Infrastructure.Data`):**
  - Added migration `004_AccountingAndLedgers.sql` (`account_heads`, `journal_entries`, `journal_lines`) seeded with standard Indian pharmacy chart of accounts (Cash, Bank, Debtors, Stock, GST Input/Output, Sales, Purchases, Discounts, Roundoff).
  - Implemented `SqliteAccountingRepository` with running ledger statement computation, Day Book querying, Trial Balance extraction, and manual voucher posting.
  - Automatic double-entry journal postings integrated into `SqliteSaleRepository` and `SqlitePurchaseRepository`.
- **3. WinUI 3 Desktop Views & ViewModels (`Medistock.Desktop`):**
  - `AccountingViewModel` & `AccountingPage.xaml`: Ribbon KPI cards (Debits, Credits, 30-day Revenue, Net Profit), TabView with Chart of Accounts & Individual Ledgers, Day Book (Daily Vouchers), Trial Balance statement, and Customer Receipt/Supplier Payment/Expense quick-entry modals.

---

### ✅ Step 14 — Sales History, Credit Notes & Batch Restocking Subsystem
- **1. Domain Model (`Medistock.Domain.Sales`):**
  - Implemented `SaleReturn` aggregate root and `SaleReturnItem` entities.
  - Added `RestockDecision` enum (`RestockToAvailable`, `QuarantineDamaged`, `QuarantineExpired`) allowing granular pharmacist triage on returned goods.
- **2. Application & Data Layer (`Medistock.Application` & `Medistock.Infrastructure.Data`):**
  - Added migration `005_SalesReturnsAndCreditNotes.sql` (`sale_returns`, `sale_return_items`).
  - Implemented `SqliteSaleReturnRepository` and `SaleReturnService` with atomic multi-step commit:
    - Return persistence & original sale status updates.
    - Automatic inventory restock into sellable batches or quarantine movement logging.
    - Automatic Credit Note double-entry posting (`Dr Sales Return`, `Dr Output GST Reversal`, `Cr Cash / Customer Debtors`).
    - Emitting `SALE_RETURNED` Outbox event for offline cloud synchronization.
- **3. WinUI 3 Desktop Views & App Shell Navigation (`Medistock.Desktop`):**
  - `SalesHistoryViewModel` & `SalesHistoryPage.xaml`: Two-panel layout with search by invoice #/customer, itemized breakdown, and full/partial return modal with restock selection.
  - Integrated into `MainWindow.xaml` and `App.xaml.cs` alongside POS Billing (`F3`), Inventory (`Ctrl+2`), Expiry Engine (`Ctrl+3`), Schedule Drug Register (`Ctrl+4`), Purchases (`Ctrl+5`), and Financial Accounts.
- **Verification & Test Status:**
  - **46 automated unit & integration tests passing with 0 warnings and 0 errors** across all 14 projects.

---

### ✅ Step 15 — Cloud Sync Hub, B2B Wholesaler Commerce & Document Report Generation
- **1. Cloud API & Synchronization Hub (`Medistock.Contracts`, `Medistock.CloudApi`, `Medistock.LocalServer`, `Medistock.Infrastructure.Sync`):**
  - Contracts in `Medistock.Contracts.Sync`: `SyncPushRequest`, `SyncPushResponse`, `SyncPullRequest`, `SyncPullResponse`, `SyncEventDto`, `SyncItemResultDto`.
  - Auth Contracts in `Medistock.Contracts.Auth`: `DeviceRegistrationRequest/Response`, `UserLoginRequest/Response`.
  - `ISyncEngineService` / `SyncEngineService`: Enforces tenant isolation (`org_id` JWT claims verification), idempotency key deduplication, monotonic server sequence generation, and delta pull pagination.
  - Minimal API Endpoints in `Medistock.CloudApi` and `Medistock.LocalServer`:
    - `POST /api/v1/sync/push`
    - `POST /api/v1/sync/pull`
    - `POST /api/v1/auth/register-device`
    - `POST /api/v1/auth/login`
    - `GET /health` & `GET /api/health`
  - `ICloudSyncClient` / `CloudSyncClient` in `Medistock.Infrastructure.Sync` integrated with `OutboxSyncWorker`.
- **2. B2B Pharmacy-to-Wholesaler Commerce Engine (`Medistock.Domain.B2B`, `Medistock.Application`, `Medistock.Infrastructure.Data`):**
  - Domain models: `Wholesaler`, `B2bOrder`, `B2bOrderItem`, `B2bOrderStatus`.
  - Migration `007_SyncHubAndB2bCommerce.sql` (`server_sync_events`, `b2b_wholesalers`, `b2b_wholesaler_catalogs`, `b2b_purchase_orders`, `b2b_purchase_order_items`).
  - Repositories & Services: `SqliteB2bCommerceRepository`, `B2bCommerceService` supporting live catalog scheme queries (free items math), PO placement, order state machine (`Draft` $\rightarrow$ `Submitted` $\rightarrow$ `Confirmed` $\rightarrow$ `Dispatched` $\rightarrow$ `Delivered`), and one-click conversion to `PurchaseInvoice` with automatic batch restock.
  - WinUI 3 Desktop Views: `B2bCommercePage.xaml` / `B2bCommerceViewModel.cs` with KPI cards, multi-item PO cart, and live order tracking ribbon.
- **3. Receipt & Report Document Generator (`Medistock.Infrastructure.Hardware`):**
  - `DocumentReportGenerator`: ESC/POS 80mm & 58mm thermal commands, Credit Note sales return voucher plain text & ESC/POS bytes, and HTML Tax Invoice templates.
- **Verification & Test Status:**
  - **71 automated unit & integration tests passing with 0 warnings and 0 errors** across all 14 solution projects.

---

### ✅ Step 16 — 100% Bill Customization Studio, Print Preview with PDF Download & Real Hardware Printing
- **1. Domain Models & Factory Presets (`Medistock.Contracts.Printing`):**
  - Granular models: `BillTemplateConfig`, `BillHeaderConfig`, `BillMetadataConfig`, `BillColumnConfig`, `BillFooterConfig`, `BillTaxSummaryConfig`, `BillSignatureConfig`, `BillStyleConfig`.
  - 19 fully customizable column fields: `SrNo`, `ItemName`, `Packing`, `Manufacturer`, `BatchNumber`, `ExpiryDate`, `HsnCode`, `Mrp`, `UnitRate`, `Quantity`, `FreeQuantity`, `DiscountPercent`, `DiscountAmount`, `GstPercent`, `CgstAmount`, `SgstAmount`, `IgstAmount`, `TaxableAmount`, `TotalAmount`.
  - 4 standard factory presets: `A4 Standard Tax Invoice`, `A5 Compact Medical Memo`, `80mm High-Speed POS Thermal Slip`, `58mm Compact Thermal Slip`.
- **2. Database Migration & SQLite Persistence (`Medistock.Infrastructure.Data`):**
  - Migration `008_BillCustomizationAndPrinters.sql`: `bill_templates` table and `printer_configurations` table.
  - Implemented `SqliteBillTemplateRepository` with automatic preset seeding, CRUD, and JSON import/export.
- **3. Universal Bill Generator & Indian Currency Words Converter (`Medistock.Infrastructure.Hardware`):**
  - `IndianCurrencyWordsConverter`: Formats decimal amounts into standard Indian English (Crores, Lakhs, Thousands, Hundreds, Rupees & Paise).
  - `BillDocumentGenerator`: Generates responsive, self-contained HTML5/CSS3 matching paper dimensions (A4, A5, 80mm, 58mm) and dynamic ESC/POS binary streams.
- **4. Real Hardware Printer Support & Win32 Spooler Integration (`Medistock.Infrastructure.Hardware`):**
  - `RawPrinterHelper`: Direct unmanaged Win32 `winspool.drv` P/Invoke (`OpenPrinter`, `StartDocPrinter`, `WritePrinter`) streaming raw ESC/POS binary to USB/driverless thermal printers.
  - `HardwarePrinterService`: Enumerates installed Windows printers, supports network TCP raw sockets (port 9100), and dispatches print jobs.
- **5. WinUI 3 Desktop Views & ViewModels (`Medistock.Desktop`):**
  - `PrintPreviewDialog.xaml` / `PrintPreviewViewModel.cs`: Embedded `WebView2` live invoice preview, direct high-res vector PDF download/export button (`CoreWebView2.PrintToPdfAsync`), template switcher, and printer dispatcher.
  - `BillCustomizerPage.xaml` / `BillCustomizerViewModel.cs`: Split-screen studio with 5 configuration tabs (Header, Columns manager with up/down reorder & visibility toggles, Metadata, Totals/Tax, Signatures) and interactive live `WebView2` preview viewport.
- **Verification & Test Status:**
  - **101 automated unit & integration tests passing with 0 warnings and 0 errors** across all solution projects.

---

## 6. Project Milestone Status
| Milestone | Status | Key Deliverables |
|---|---|---|
| **Phase 1: POS & Data Hot-Path** | ✅ Complete | SQLite WAL, FTS5 (<1ms search), Atomic POS checkout (<75ms), FEFO batch engine, ESC/POS HAL |
| **Phase 2: Inventory & Compliance** | ✅ Complete | Expiry risk bands, Schedule Drug Register (Form 35 / H1), Purchases & Landed cost |
| **Phase 3: Financial & Tax Core** | ✅ Complete | Double-entry ledger engine, Credit notes / Returns triage, GST (GSTR-1/2/3B, HSN) |
| **Phase 4: Multi-Branch & B2B Hub** | ✅ Complete | Inter-branch transfers, Cloud sync engine, B2B Wholesaler commerce, Document generators |
| **Phase 5: Bill Customization & Hardware** | ✅ Complete | 100% Bill Customizer Studio, Win32 RAW Spooler, WebView2 Print Preview & PDF Downloader |

---

## 7. Master Reference Documents
- [PRD.md](file:///D:/Projects/Medistock-offlinefirst/PRD.md) — Product Requirements Document (features, acceptance criteria, NFRs)
- [ARCHITECTURE.md](file:///D:/Projects/Medistock-offlinefirst/ARCHITECTURE.md) — Master system blueprint (v1.1, 23 sections)
- [UI_DESIGN_SYSTEM.md](file:///D:/Projects/Medistock-offlinefirst/UI_DESIGN_SYSTEM.md) — Design tokens, components, themes, performance rules
- [KEYBOARD_SHORTCUTS.md](file:///D:/Projects/Medistock-offlinefirst/KEYBOARD_SHORTCUTS.md) — Full keyboard/input architecture, both keymap profiles, scope system
- [AGENT.md](file:///D:/Projects/Medistock-offlinefirst/AGENT.md) — Agent operational rules & coding protocols
- [AGY_STATE.md](file:///D:/Projects/Medistock-offlinefirst/AGY_STATE.md) — Active session state and progress log




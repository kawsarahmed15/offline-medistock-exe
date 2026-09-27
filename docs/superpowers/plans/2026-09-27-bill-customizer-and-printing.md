# 100% Configurable Bill Customization, Print Preview with PDF Download & Real Printer Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide 100% customizable pharmacy bill templates (columns, headers, metadata, footers, tax tables, signatures, paper sizes), embedded WebView2 print preview with direct PDF download, and real Windows/ESC-POS hardware printer integration.

**Architecture:** Clean Architecture + MVVM. Dynamic HTML5/CSS3 & ESC/POS document generation engine, Win32 `winspool.drv` RawPrinterHelper for driverless USB thermal receipt printers, TCP socket for network printers, and WinUI 3 WebView2 for live print preview with native `PrintToPdfAsync` export.

**Tech Stack:** C#, .NET 9 LTS, WinUI 3 / Windows App SDK, Microsoft.Web.WebView2, SQLite / Dapper, Win32 Spooler P/Invoke, xUnit / FluentAssertions.

**Spec:** [`docs/superpowers/specs/2026-09-27-bill-customizer-and-printing-design.md`](file:///D:/Projects/Medistock-offlinefirst/docs/superpowers/specs/2026-09-27-bill-customizer-and-printing-design.md)

## Global Constraints
- Target Framework: `net9.0` (Core/Shared/Tests) and `net9.0-windows10.0.26100.0` (Desktop Client).
- No hardcoded colors or text in components; use design system tokens and customizable template properties.
- Offline-first: PDF generation, preview, ESC/POS, and raw spooling must execute with 0 cloud dependencies.
- Zero data loss: All template changes persisted locally in SQLite and exportable to JSON.

## Review Focus
- **Column Visibility & Reordering:** Hiding any column or changing order must correctly re-render HTML table headers and cells without misaligning data.
- **INR Amount to Words:** Decimal paise (e.g. ₹1,250.75) and zero-fraction amounts (e.g. ₹5,000.00) must format correctly into Indian numbering syntax.
- **Thermal ESC/POS vs A4 Scaling:** Template rendering must dynamically scale between 80mm/58mm thermal slips and A4/A5 full-page layouts.
- **Raw Spooler Error Resilience:** Win32 `OpenPrinter`/`WritePrinter` failures (e.g. printer unplugged/offline) must return clear error results rather than crashing the application.
- **PDF Export Path Handling:** Writing PDF via `PrintToPdfAsync` must handle invalid file paths, existing file overwrites, and permission barriers gracefully.

---

### Task 1: Domain Configuration Models & Factory Presets

**Files:**
- Create: `src/Shared/Medistock.Contracts/Printing/BillTemplateModels.cs`
- Create: `src/Shared/Medistock.Contracts/Printing/BillTemplatePresets.cs`
- Test: `tests/Medistock.Domain.Tests/BillTemplateTests.cs`

**Interfaces:**
- Produces: `BillTemplateConfig`, `BillHeaderConfig`, `BillColumnConfig`, `BillMetadataConfig`, `BillFooterConfig`, `BillTaxSummaryConfig`, `BillSignatureConfig`, `BillStyleConfig`, `PaperSize`, `PrinterInterfaceType`, `BillFieldType`, `BillTemplatePresets`.

- [ ] **Step 1: Write failing unit tests for BillTemplate models and presets**
- [ ] **Step 2: Run tests to verify failure**
- [ ] **Step 3: Implement `BillTemplateModels.cs` and `BillTemplatePresets.cs`**
- [ ] **Step 4: Run tests to verify PASS**
- [ ] **Step 5: Commit changes**

---

### Task 2: Database Migration & SQLite Template Persistence

**Files:**
- Create: `src/Infrastructure/Medistock.Infrastructure.Data/Migrations/008_BillCustomizationAndPrinters.sql`
- Create: `src/Core/Medistock.Application/Common/Interfaces/IBillTemplateRepository.cs`
- Create: `src/Infrastructure/Medistock.Infrastructure.Data/Repositories/SqliteBillTemplateRepository.cs`
- Modify: `src/Infrastructure/Medistock.Infrastructure.Data/DependencyInjection.cs`
- Test: `tests/Medistock.Infrastructure.Tests/BillTemplateRepositoryTests.cs`

**Interfaces:**
- Consumes: `BillTemplateConfig`, `ISqliteConnectionFactory`
- Produces: `IBillTemplateRepository` with `GetDefaultTemplateAsync()`, `GetTemplateByIdAsync()`, `SaveTemplateAsync()`, `ListAllTemplatesAsync()`, `SetDefaultTemplateAsync()`, `DeleteTemplateAsync()`.

- [ ] **Step 1: Write migration `008_BillCustomizationAndPrinters.sql`**
- [ ] **Step 2: Write failing repository integration tests**
- [ ] **Step 3: Implement `IBillTemplateRepository` and `SqliteBillTemplateRepository` with Dapper**
- [ ] **Step 4: Register in Dependency Injection**
- [ ] **Step 5: Run tests to verify PASS**
- [ ] **Step 6: Commit changes**

---

### Task 3: Universal Bill Document Generator & Number-to-Words Engine

**Files:**
- Create: `src/Infrastructure/Medistock.Infrastructure.Hardware/Printers/IBillDocumentGenerator.cs`
- Create: `src/Infrastructure/Medistock.Infrastructure.Hardware/Printers/BillDocumentGenerator.cs`
- Create: `src/Infrastructure/Medistock.Infrastructure.Hardware/Printers/IndianCurrencyWordsConverter.cs`
- Modify: `src/Infrastructure/Medistock.Infrastructure.Hardware/DependencyInjection.cs`
- Test: `tests/Medistock.Infrastructure.Tests/BillDocumentGeneratorTests.cs`

**Interfaces:**
- Consumes: `SaleReceiptModel`, `BillTemplateConfig`
- Produces: `IBillDocumentGenerator` with `GenerateHtmlBillAsync()`, `GenerateEscPosBillAsync()`, `ConvertAmountToWordsInrAsync()`.

- [ ] **Step 1: Write unit tests for currency words conversion and dynamic HTML/ESC-POS generation**
- [ ] **Step 2: Run tests to verify failure**
- [ ] **Step 3: Implement `IndianCurrencyWordsConverter.cs`**
- [ ] **Step 4: Implement `BillDocumentGenerator.cs` supporting all 19 columns, dynamic headers, metadata, tax summary, and footer signatures**
- [ ] **Step 5: Register in `Medistock.Infrastructure.Hardware/DependencyInjection.cs`**
- [ ] **Step 6: Run tests to verify PASS**
- [ ] **Step 7: Commit changes**

---

### Task 4: Hardware Printer Service & Win32 Spooler Integration

**Files:**
- Create: `src/Infrastructure/Medistock.Infrastructure.Hardware/Printers/RawPrinterHelper.cs`
- Create: `src/Infrastructure/Medistock.Infrastructure.Hardware/Printers/IHardwarePrinterService.cs`
- Create: `src/Infrastructure/Medistock.Infrastructure.Hardware/Printers/HardwarePrinterService.cs`
- Modify: `src/Infrastructure/Medistock.Infrastructure.Hardware/DependencyInjection.cs`
- Test: `tests/Medistock.Infrastructure.Tests/HardwarePrinterServiceTests.cs`

**Interfaces:**
- Produces: `IHardwarePrinterService` with `GetInstalledPrintersAsync()`, `PrintRawEscPosAsync()`, `PrintNetworkTcpAsync()`, `PrintReceiptAsync()`.

- [ ] **Step 1: Write unit tests for printer service and printer enumeration**
- [ ] **Step 2: Implement `RawPrinterHelper.cs` using Win32 `winspool.drv` P/Invoke**
- [ ] **Step 3: Implement `HardwarePrinterService.cs` with support for Windows Spooler RAW, Network TCP, and driver printing**
- [ ] **Step 4: Register in `Medistock.Infrastructure.Hardware/DependencyInjection.cs`**
- [ ] **Step 5: Run tests to verify PASS**
- [ ] **Step 6: Commit changes**

---

### Task 5: Print Preview & PDF Downloader Dialog (WinUI 3)

**Files:**
- Create: `src/Clients/Medistock.Desktop/Views/PrintPreviewDialog.xaml`
- Create: `src/Clients/Medistock.Desktop/Views/PrintPreviewDialog.xaml.cs`
- Create: `src/Clients/Medistock.Desktop/ViewModels/PrintPreviewViewModel.cs`
- Modify: `src/Clients/Medistock.Desktop/ViewModels/PosViewModel.cs`
- Modify: `src/Clients/Medistock.Desktop/ViewModels/SalesHistoryViewModel.cs`
- Modify: `src/Clients/Medistock.Desktop/DependencyInjection.cs`
- Test: `tests/Medistock.Desktop.Tests/PrintPreviewViewModelTests.cs`

**Interfaces:**
- Consumes: `IBillDocumentGenerator`, `IBillTemplateRepository`, `IHardwarePrinterService`
- Produces: `PrintPreviewDialog` with embedded WebView2 live preview, PDF file download, and hardware print dispatcher.

- [ ] **Step 1: Write ViewModel unit tests for `PrintPreviewViewModel`**
- [ ] **Step 2: Implement `PrintPreviewViewModel.cs`**
- [ ] **Step 3: Implement `PrintPreviewDialog.xaml` and `PrintPreviewDialog.xaml.cs` with WebView2 and PDF export handler**
- [ ] **Step 4: Wire "Print Preview" / "Print Bill" into `PosViewModel.cs` and `SalesHistoryViewModel.cs`**
- [ ] **Step 5: Run tests to verify PASS**
- [ ] **Step 6: Commit changes**

---

### Task 6: 100% Bill Customization Studio (WinUI 3)

**Files:**
- Create: `src/Clients/Medistock.Desktop/Views/BillCustomizerPage.xaml`
- Create: `src/Clients/Medistock.Desktop/Views/BillCustomizerPage.xaml.cs`
- Create: `src/Clients/Medistock.Desktop/ViewModels/BillCustomizerViewModel.cs`
- Modify: `src/Clients/Medistock.Desktop/Views/MainWindow.xaml`
- Modify: `src/Clients/Medistock.Desktop/Views/MainWindow.xaml.cs`
- Modify: `src/Clients/Medistock.Desktop/Views/SettingsPage.xaml`
- Modify: `src/Clients/Medistock.Desktop/Views/SettingsPage.xaml.cs`
- Test: `tests/Medistock.Desktop.Tests/BillCustomizerViewModelTests.cs`

**Interfaces:**
- Consumes: `IBillTemplateRepository`, `IBillDocumentGenerator`
- Produces: Full interactive split-screen editor for all template properties (styling, header, metadata, 19 columns, totals, signatures) with instant live preview.

- [ ] **Step 1: Write ViewModel unit tests for `BillCustomizerViewModel`**
- [ ] **Step 2: Implement `BillCustomizerViewModel.cs` (property bindings, column toggles, reordering, debounced preview refresh, save/export/import)**
- [ ] **Step 3: Implement `BillCustomizerPage.xaml` and `.cs` (Tabbed editor left, WebView2 live preview right)**
- [ ] **Step 4: Add navigation entry in `MainWindow.xaml` and `SettingsPage.xaml`**
- [ ] **Step 5: Run tests to verify PASS**
- [ ] **Step 6: Commit changes**

---

### Task 7: End-to-End Verification & Solution Build

**Files:**
- Test: All tests in `tests/Medistock.Application.Tests`, `tests/Medistock.Desktop.Tests`, `tests/Medistock.Domain.Tests`, `tests/Medistock.Infrastructure.Tests`

- [ ] **Step 1: Run `dotnet test Medistock.sln` to ensure 100% test pass rate**
- [ ] **Step 2: Run `dotnet build Medistock.sln` to confirm 0 compilation errors and warnings**
- [ ] **Step 3: Update `AGY_STATE.md` with completed deliverables**
- [ ] **Step 4: Commit final changes**

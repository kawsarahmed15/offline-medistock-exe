# Medistock — Keyboard Shortcut System & Input Architecture

> **Version:** 1.0 | **Date:** 2026-09-24 | **Status:** Authoritative Reference  
> **Source:** MARG ERP 9+ official shortcut documentation (care.margcompusoft.com) + Medistock design decisions  
> **Critical Rule:** Shortcuts are a config layer on top of Commands — never wire keys directly to business logic.

---

## TABLE OF CONTENTS
1. [Architecture: Command Layer First](#1-architecture-command-layer-first)
2. [Keymap Profiles (User Selectable)](#2-keymap-profiles-user-selectable)
3. [Scope System (Context-Aware Keys)](#3-scope-system-context-aware-keys)
4. [Global Shortcuts (All Screens)](#4-global-shortcuts-all-screens)
5. [POS Screen Shortcuts](#5-pos-screen-shortcuts)
6. [Payment Screen Shortcuts](#6-payment-screen-shortcuts)
7. [Purchase Entry Shortcuts](#7-purchase-entry-shortcuts)
8. [Accounting / Voucher Shortcuts](#8-accounting--voucher-shortcuts)
9. [Product / Inventory Shortcuts](#9-product--inventory-shortcuts)
10. [Reports Shortcuts](#10-reports-shortcuts)
11. [Conflict Resolution Decisions](#11-conflict-resolution-decisions)
12. [In-App Shortcut Help Overlay](#12-in-app-shortcut-help-overlay)
13. [Mouse Parity Rules](#13-mouse-parity-rules)
14. [Implementation: WinUI 3 KeyboardAccelerators](#14-implementation-winui-3-keyboardaccelerators)
15. [Shortcut Testing Checklist](#15-shortcut-testing-checklist)

---

## 1. Architecture: Command Layer First

> ⚠️ **Critical architectural rule from** `shortcut-mapping.md` **Section 11:**  
> "Build the command layer first, the keymap second. Commands like `SaveBillCommand`, `ApplyDiscountCommand` should exist independently of which key triggers them. The shortcut table becomes a JSON/config binding layer on top of commands — not shortcuts wired directly into business logic."

### Command Architecture

```
INPUT
  ├── Keyboard (shortcut)     ─┐
  ├── Mouse (button click)    ─┤──► ICommand (e.g. SaveSaleCommand)
  ├── Barcode scanner         ─┤         │
  └── Touch / stylus          ─┘         ▼
                                   Business Logic
                                         │
                                         ▼
                                   Domain Service
```

Every action in the system has a named `ICommand`. Shortcuts, buttons, context menus, and command palette entries all invoke the **same command**. No shortcut ever directly calls a database or service method.

### Command Naming Convention

```csharp
// POS Commands
SaveSaleCommand
ApplyDiscountCommand
OpenPaymentCommand
HoldBillCommand
ResumeBillCommand
NewSaleCommand
SaleReturnCommand
OpenCustomerCommand
PrintInvoiceCommand
CancelSaleCommand

// Purchase Commands
NewPurchaseCommand
SavePurchaseCommand
PostPurchaseInvoiceCommand
AddPurchaseItemRowCommand

// Accounting Commands
OpenVoucherCommand
SwitchToPaymentVoucherCommand
SwitchToReceiptVoucherCommand
SwitchToJournalVoucherCommand
SwitchToDebitNoteCommand
SwitchToCreditNoteCommand
SwitchToContraCommand

// Global Commands
OpenCommandPaletteCommand
OpenCalculatorCommand
OpenCalendarCommand
ChangeUserCommand
ToggleSidebarCommand
OpenShortcutHelpCommand
```

### Keymap as Config (JSON)

```json
// profiles/default.json (Medistock Standard)
{
  "profile": "default",
  "displayName": "Medistock Standard",
  "bindings": {
    "global": {
      "Ctrl+K":       "OpenCommandPaletteCommand",
      "Ctrl+\\\":      "ToggleSidebarCommand",
      "F12":          "OpenCalculatorCommand",
      "Alt+F1":       "OpenShortcutHelpCommand",
      "Ctrl+U":       "ChangeUserCommand",
      "Ctrl+Q":       "QuitCommand"
    },
    "pos": {
      "F2":           "NewSaleCommand",
      "F3":           "OpenProductSearchCommand",
      "F4":           "ApplyDiscountCommand",
      "F5":           "HoldBillCommand",
      "F6":           "OpenPaymentCommand",
      "F7":           "SaleReturnCommand",
      "F8":           "OpenCustomerCommand",
      "F9":           "PrintInvoiceCommand",
      "Ctrl+W":       "SaveSaleCommand",
      "Ctrl+S":       "SaveSaleCommand",
      "Ctrl+H":       "ResumeBillCommand",
      "Ctrl+T":       "CopyBillCommand",
      "Escape":       "CancelOrBackCommand"
    }
  }
}

// profiles/marg-compatible.json
{
  "profile": "marg-compatible",
  "displayName": "MARG-Compatible",
  "bindings": {
    "global": {
      "Ctrl+S":       "FlushCacheCommand",   // MARG: Flush Memory
      "Ctrl+U":       "ChangeUserCommand",
      "F12":          "OpenCalculatorCommand",
      "Ctrl+J":       "SwitchWindowCommand",
      "Ctrl+Q":       "QuitCommand",
      "Shift+F12":    "OpenCalendarCommand",
      "Alt+V":        "OpenVoucherCommand"
    },
    "pos": {
      "Alt+N":        "NewSaleCommand",
      "Alt+A":        "NewCounterSaleCommand",
      "Ctrl+W":       "SaveSaleCommand",
      "F4":           "ApplyDiscountCommand",
      "F11":          "CalculateChangeCommand",
      "Asterisk":     "SaleReturnCommand",
      "Alt+L":        "ViewLastDealCommand",
      "Ctrl+P":       "LoadPendingCommand",
      "F10":          "ViewTaxDetailCommand",
      "Alt+F10":      "ViewProfitCommand"   // Permission-gated
    }
  }
}
```

---

## 2. Keymap Profiles (User Selectable)

Available at: **Settings → Keyboard → Keymap Profile**

| Profile | Description | Best For |
|---|---|---|
| **Medistock Standard** (default) | Modern Windows conventions (Ctrl+S = Save) + Medistock additions | New users, non-MARG users, new hires |
| **MARG-Compatible** | Full MARG ERP 9+ key parity (Ctrl+W = Save, Ctrl+S = Flush) | Migrating MARG users, MARG-trained pharmacists |
| **Custom** | User-defined; built on top of Standard | Power users with specific preferences |

### Profile Switcher UI

```
Settings → Keyboard
─────────────────────────────────────────────
Keymap Profile
  [● Medistock Standard]
  [○ MARG-Compatible]         ← "Matches MARG ERP 9+"
  [○ Custom]

  [View all shortcuts for this profile →]
  
⚠ Changing profile applies immediately.
  Your custom bindings are preserved separately.
─────────────────────────────────────────────
```

**Profile is applied instantly** — no restart. All `KeyboardAccelerator` bindings are rebuilt from the active JSON profile at runtime.

---

## 3. Scope System (Context-Aware Keys)

Keys are scoped per **active view context**. The same physical key does different things in different screens — exactly like MARG (F2 = New Sale on POS, but F2 = New Item Row in purchase entry).

### Scope Hierarchy

```
GLOBAL SCOPE           (always active, lowest priority)
    ↑ overridden by
SCREEN SCOPE           (active module: POS, Purchase, Accounting...)
    ↑ overridden by
PANEL SCOPE            (active panel within screen: search box, cart, payment)
    ↑ overridden by
FIELD SCOPE            (specific field has focus: qty field, rate field)
```

### Scope Registration Pattern (C#)

```csharp
// Each screen registers its local scope on navigation
public class PosViewModel : IShortcutScope
{
    public string ScopeName => "pos";
    
    public void RegisterShortcuts(IShortcutRegistry registry)
    {
        registry.Register("pos", "F2", NewSaleCommand);
        registry.Register("pos", "F3", OpenSearchCommand);
        registry.Register("pos", "F4", ApplyDiscountCommand);
        registry.Register("pos.qty-field", "F2", ViewTaxStatusCommand);
        registry.Register("pos.qty-field", "F3", ChangeDiscountCommand);
    }
}
```

### Scope Conflict Resolution

When the same key is registered in multiple active scopes, **deepest (most specific) scope wins**:

```
F2 pressed while qty field is focused on POS screen:
  1. Field scope "pos.qty-field" → F2 = ViewTaxStatusCommand ✓ (wins)
  2. Screen scope "pos"          → F2 = NewSaleCommand (ignored)
  3. Global scope                → F2 = (nothing registered) (ignored)
```

---

## 4. Global Shortcuts (All Screens)

These are active in **every context** unless a deeper scope overrides them.

### Medistock Standard Profile

| Shortcut | Action | Command | Notes |
|---|---|---|---|
| `Ctrl+K` | Open Command Palette | `OpenCommandPaletteCommand` | Global search — highest ROI shortcut |
| `Ctrl+\` | Toggle Sidebar | `ToggleSidebarCommand` | Collapse/expand left nav |
| `F12` | Calculator | `OpenCalculatorCommand` | Floating overlay calculator; cashiers use this constantly |
| `Alt+F1` | Shortcut Help Overlay | `OpenShortcutHelpCommand` | Shows all shortcuts for current scope |
| `Ctrl+U` | Change User (quick switch) | `ChangeUserCommand` | PIN prompt, doesn't close current bill |
| `Ctrl+Q` | Quit Application | `QuitCommand` | Confirmation dialog if unsaved draft exists |
| `F1` | Contextual Help | `OpenContextHelpCommand` | Opens help for current screen |
| `Ctrl+,` | Open Settings | `OpenSettingsCommand` | Standard Windows convention |
| `Ctrl+Z` | Undo (where applicable) | `UndoCommand` | Scoped — only active on screens that support undo |

### MARG-Compatible Profile (Global differences)

| Shortcut | Action | Command | Differs from Standard? |
|---|---|---|---|
| `Ctrl+S` | **Flush Cache** (NOT save) | `FlushCacheCommand` | ⚠️ Yes — see Section 11 |
| `Ctrl+J` | Switch Window | `SwitchWindowCommand` | MARG-specific |
| `Ctrl+F1` | Personal Directory | `OpenPersonalDirectoryCommand` | MARG-specific |
| `Ctrl+F9` | Bill Adjustments | `OpenBillAdjustmentsCommand` | Available in both profiles |
| `Ctrl+F11` | Printer Setup | `OpenPrinterSetupCommand` | Available in both profiles |
| `Shift+F12` | Calendar | `OpenCalendarCommand` | MARG-specific; Medistock uses F12 for calculator |
| `Ctrl+F12` | Standard Narration | `OpenStandardNarrationCommand` | MARG-specific |
| `Alt+V` | Open Voucher Entry | `OpenVoucherCommand` | Available in both profiles |
| `F1` | Change Company | `ChangeCompanyCommand` | MARG-specific (conflicts with Help in Standard) |

---

## 5. POS Screen Shortcuts

> This is the **highest-frequency screen** in the entire application. Every key here will be pressed thousands of times per cashier per day. Get this perfect before any other screen.

### Medistock Standard — POS Main Context

| Shortcut | Action | Command | Priority |
|---|---|---|---|
| `F2` | **New Sale** | `NewSaleCommand` | 🔴 Critical |
| `F3` | **Product Search / Focus search box** | `OpenProductSearchCommand` | 🔴 Critical |
| `F4` | **Apply Bill Discount** | `ApplyDiscountCommand` | 🔴 Critical — permission-gated |
| `F5` | **Hold Bill** | `HoldBillCommand` | 🟡 High |
| `F6` | **Open Payment Screen** | `OpenPaymentCommand` | 🔴 Critical |
| `F7` | **Sale Return on this bill** | `SaleReturnCommand` | 🟡 High |
| `F8` | **Open Customer Lookup** | `OpenCustomerCommand` | 🟡 High |
| `F9` | **Print Invoice** | `PrintInvoiceCommand` | 🟡 High |
| `Ctrl+W` | **Save Bill** | `SaveSaleCommand` | 🔴 Critical |
| `Ctrl+S` | **Save Bill** (Standard profile) | `SaveSaleCommand` | 🔴 Critical |
| `Ctrl+H` | **Resume Held Bill** | `ResumeBillCommand` | 🟡 High |
| `Ctrl+T` | **Copy Bill** | `CopyBillCommand` | 🟢 Normal |
| `Ctrl+P` | **Load Pending Orders** | `LoadPendingCommand` | 🟢 Normal |
| `Ctrl+R` | **Reload Tax** | `ReloadTaxCommand` | 🟢 Normal |
| `F11` | **Calculate Change Amount** | `CalculateChangeCommand` | 🟡 High |
| `F10` | **View Tax Detail Breakdown** | `ViewTaxDetailCommand` | 🟢 Normal |
| `Alt+F10` | **View Profit on bill** | `ViewProfitCommand` | 🔒 Permission-gated (Manager+) |
| `Left Arrow` | **Previous Bill Number** | `PreviousBillCommand` | 🟢 Normal |
| `Escape` | **Cancel / Back** | `CancelOrBackCommand` | 🔴 Critical |
| `Alt+F11` | **Save Item Set (bundle)** | `SaveItemSetCommand` | 🟢 Normal |
| `Alt+F12` | **Load Item Set (bundle)** | `LoadItemSetCommand` | 🟢 Normal |

### POS — Product Search Field Context

| Shortcut | Action | Notes |
|---|---|---|
| Any printable character | Types into search box | Auto-focuses search field on first key after bill creation |
| `Enter` | Add top result to cart | Or if item already selected: confirm selection |
| `Down Arrow` | Move focus to results list | |
| `Up Arrow` | Move focus back to search box | From results list |
| `Tab` | Move to quantity field of added item | |
| `Escape` | Clear search / close results | |

### POS — Quantity Field Context

| Shortcut | Action | Command | Source |
|---|---|---|---|
| `F2` | View item tax status | `ViewItemTaxStatusCommand` | MARG parity |
| `F3` | Change deal/discount for this item | `ChangeItemDealCommand` | MARG parity |
| `F5` / `*` | Change net rate | `ChangeNetRateCommand` | MARG parity |
| `F7` | Change lot rate | `ChangeLotRateCommand` | MARG parity |
| `+` | Change margin | `ChangeMarginCommand` | MARG parity |
| `Ctrl+Tab` | View batch detail | `ViewBatchDetailCommand` | MARG parity |
| `Tab` | Move to next item row / confirm qty | | Standard |
| `Enter` | Confirm quantity and move on | | Standard |
| `Escape` | Cancel quantity change | | Standard |

### MARG-Compatible Profile — POS Differences

| Shortcut | Action | Notes |
|---|---|---|
| `Alt+N` | New Sale Bill | MARG primary new-bill key |
| `Alt+A` | New Counter Sale | MARG counter-sale shortcut |
| `Alt+C` | New Sale Challan | MARG-specific |
| `Alt+M` | Modify Bill | MARG-specific |
| `Ctrl+F2` | Switch batch-pick mode (Manual/Self/FIFO) | MARG-specific |
| `Ctrl+F3` | Modify Bill (alternate) | MARG-specific |
| `Alt+L` | View Last Deal of Item | MARG-specific — shown in context tooltip |
| `Shift+~` | View Item Cost/Profit | 🔒 Permission-gated |
| `*` | Sale Return / Breakage / Replacement | MARG asterisk shortcut |
| `/` | Bill Conversion | MARG-specific |
| `Alt+~` | Open Message Window | MARG-specific |
| `Alt+Insert` | Switch to Cash Challan | MARG-specific |
| `Ctrl+Y` | Switch to Purchase Challan | MARG-specific |
| `Ctrl+A` | Switch to Counter Sale | MARG-specific |
| `Ctrl+D` | Switch to Stock Receive | MARG-specific |

---

## 6. Payment Screen Shortcuts

Payment screen opens on `F6` from POS. Keyboard flow:

| Shortcut | Action | Notes |
|---|---|---|
| `1` | Select Cash payment mode | Number keys select mode |
| `2` | Select Card payment mode | |
| `3` | Select UPI payment mode | |
| `4` | Select Credit (against ledger) | |
| `5` | Select Split payment | |
| Any digit | Types into "Amount Received" field | Auto-focused on screen open |
| `F11` | Calculate exact change | |
| `F6` / `Enter` / `Ctrl+W` | **Complete Sale** | Highest-frequency action on this screen |
| `Escape` / `Backspace` | Back to POS | Without saving |

### Payment Flow (Keyboard-Only, Zero Mouse)

```
[F6] → Payment screen opens
       Amount Received field auto-focused
[200] → types ₹200.00
[Enter] → completes sale → prints invoice → clears bill → focus on search box
```

This entire flow: **4 keystrokes** from bill-ready to receipt printed.

---

## 7. Purchase Entry Shortcuts

### Purchase Screen Context

| Shortcut | Action | Command |
|---|---|---|
| `F2` | New purchase item row | `AddPurchaseItemRowCommand` |
| `F3` | Modify selected row | `ModifyPurchaseItemCommand` |
| `Ctrl+W` / `Ctrl+S` | Save as draft | `SavePurchaseDraftCommand` |
| `F10` | Post/Finalize invoice | `PostPurchaseInvoiceCommand` |
| `Tab` | Move between fields in item row | Standard |
| `Enter` | Confirm field / move to next | Standard |
| `Delete` | Delete selected item row | `DeletePurchaseItemCommand` |
| `Insert` | Add/less stock adjustment | `StockAdjustmentCommand` |
| `Escape` | Cancel / Back | `CancelOrBackCommand` |
| `Alt+P` | Create Purchase Bill (MARG-compat) | `NewPurchaseBillCommand` |
| `F9` | Print Purchase Order | `PrintPurchaseCommand` |

### Purchase — Barcode-First Item Entry

```
Scan/type product barcode or name
  → Product auto-fills
  → Cursor moves to Batch field [Tab auto-advance]
  → Type batch number
  → Type expiry date (DD/MM/YY, auto-formatted)
  → Type quantity
  → Type purchase rate (GST auto-calculates)
  → [Enter] confirms row, opens next row
```

---

## 8. Accounting / Voucher Shortcuts

### Voucher Screen — F-Key Pattern (MARG parity, same in both profiles)

| Shortcut | Action | Command |
|---|---|---|
| `Alt+V` | Open Voucher Entry window | `OpenVoucherCommand` |
| `F2` | Switch to Payment Voucher | `SwitchToPaymentVoucherCommand` |
| `F3` | Switch to Receipt Voucher | `SwitchToReceiptVoucherCommand` |
| `F4` | Switch to Journal Voucher | `SwitchToJournalVoucherCommand` |
| `F5` | Switch to Debit Note Voucher | `SwitchToDebitNoteCommand` |
| `F6` | Switch to Credit Note Voucher | `SwitchToCreditNoteCommand` |
| `F7` | Switch to Contra Voucher | `SwitchToContraCommand` |
| `Alt+I` | Open Single Entry | `OpenSingleEntryCommand` |
| `Alt+U` | Open Cheque/Cash | `OpenChequeCommand` |
| `Alt+Q` | Open PD Cheque/Cash | `OpenPDChequeCommand` |
| `Ctrl+W` / `Ctrl+S` | Save voucher | `SaveVoucherCommand` |
| `Escape` | Cancel / Back | `CancelOrBackCommand` |

### Ledger Screen

| Shortcut | Action |
|---|---|
| `Ctrl+L` | View Ledger window |
| `F2` | View Ledger month-wise |
| `F3` | Modify Ledger |
| `F4` | View Ledger Interest Collection |
| `F6` | View Ledger-wise Adjustment |
| `F8` | View PDC of Ledger |
| `F10` | Filter ledgers |
| `Ctrl+F1` | View Ledger Summary |
| `Alt+F1` | Load challan value in ledger |
| `Alt+L` | View All Ledgers |
| `Alt+P` | Print/Excel report |

---

## 9. Product / Inventory Shortcuts

### Product Master Screen

| Shortcut | Action | Command |
|---|---|---|
| `Ctrl+I` | Open Item/Product List | `OpenProductListCommand` |
| `F2` | Create New Product | `NewProductCommand` |
| `F3` | Modify selected Product | `ModifyProductCommand` |
| `F4` | View Item Register | `ViewItemRegisterCommand` |
| `F5` | Index / Sort items | `IndexItemsCommand` |
| `F7` | Filter by Salt/Generic | `FilterBySaltCommand` |
| `F8` | Filter by Salt (alternate) | `FilterBySaltAltCommand` |
| `F9` | Filter by Manufacturer | `FilterByManufacturerCommand` |
| `F10` | View Only Available Stock | `FilterAvailableStockCommand` |
| `Insert` | Stock Adjustment (add/less) | `StockAdjustmentCommand` |
| `Ctrl+Tab` | View Item Detail | `ViewItemDetailCommand` |
| `F6` | View Old Purchase Rate | `ViewOldPurchaseRateCommand` |
| `Alt+F1` | View Shortcut Keys (this screen) | `OpenShortcutHelpCommand` |

### Inventory Grid Screen

| Shortcut | Action |
|---|---|
| `Ctrl+F` | Filter panel toggle |
| `Ctrl+E` | Export to Excel/CSV |
| `Ctrl+A` | Select all visible rows |
| `Space` | Toggle selection on focused row |
| `Delete` | Delete selected (with confirmation) |
| `Enter` | Open detail view of focused row |
| `F5` | Refresh inventory data |

---

## 10. Reports Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+E` | Export current report (Excel/CSV/PDF) |
| `Ctrl+P` | Print current report |
| `F5` | Refresh report data |
| `Alt+P` | Print / Excel (MARG-compat) |
| `F2` | Date-wise view |
| `Ctrl+F` | Search within report |

---

## 11. Conflict Resolution Decisions

### Decision 1: Ctrl+S (most important) ✅ RESOLVED

| | Standard Profile | MARG-Compatible Profile |
|---|---|---|
| `Ctrl+S` | **Save** (universal Windows convention) | **Flush Cache** (MARG behavior) |
| `Ctrl+W` | **Save** (also mapped, MARG muscle memory) | **Save** (MARG primary save) |

**Rationale:** Mapping Ctrl+S to Flush Cache in Standard profile creates a financial-safety risk — a cashier thinking they saved a transaction when they didn't. Standard profile uses Ctrl+S = Save. MARG users who need Ctrl+W = Save also get it in the Standard profile (both work). MARG users who specifically need the original Ctrl+S = Flush behavior activate the MARG-Compatible profile.

### Decision 2: F2/F3/F4/Ctrl+Tab Context Collision ✅ RESOLVED

These keys do different things per screen. Resolved by the **Scope System** (Section 3). Each screen registers its own scope. The deepest active scope wins. No conflicts in practice.

### Decision 3: Ctrl+W = Save vs. "Pick all items" ✅ RESOLVED

MARG documentation shows Ctrl+W for both "Save Bill" and "Pick all required items at once" — this appears to be a context-dependent collision in the source docs. Decision: `Ctrl+W` = **Save Bill** in all contexts where a bill exists. The "pick all items" action is exposed as a toolbar button only (no shortcut), pending confirmation from real MARG users that this is actually a different window context.

### Decision 4: Ctrl+T = "Copy Bill" vs. "Load Quotation" ✅ RESOLVED

Decision: `Ctrl+T` = **Copy Bill**. Load Quotation is exposed via Command Palette and a toolbar button. Both MARG behaviors are preserved, but Copy Bill is the significantly more frequent action.

### Decision 5: Alt+I = "Open Single Entry" vs. "Create Duplicate Voucher" ✅ RESOLVED

These are in different screen contexts (Voucher Entry vs. Books). Resolved by scope system. Alt+I in Voucher screen = Open Single Entry. Alt+I in Books = Duplicate Voucher.

### Decision 6: F1 = Help vs. "Change Company" (MARG) ✅ RESOLVED

Standard profile: F1 = Contextual Help (universal Windows convention).  
MARG-Compatible profile: F1 = Change Company (MARG behavior).

---

## 12. In-App Shortcut Help Overlay

### Access: `Alt+F1` from anywhere

Opens a floating, non-blocking overlay showing all shortcuts **active in the current context**:

```
╔══════════════════════════════════════════════════════╗
║  Keyboard Shortcuts — POS Screen          [Alt+F1×] ║
╠══════════════════════════════════════════════════════╣
║  BILL ACTIONS                                        ║
║  F2          New Sale                                ║
║  F3          Search Product                          ║
║  F4          Apply Discount          [Manager+]      ║
║  F5          Hold Bill                               ║
║  F6          Payment                                 ║
║  F7          Sale Return                             ║
║  Ctrl+W / Ctrl+S   Save Bill                        ║
║                                                      ║
║  ITEM FIELD (when focused)                           ║
║  F2          View Tax Status                         ║
║  F3          Change Deal/Discount                    ║
║  Ctrl+Tab    View Batch Detail                       ║
║                                                      ║
║  GLOBAL                                              ║
║  Ctrl+K      Command Palette                         ║
║  F12         Calculator                              ║
║  Alt+F1      This help overlay                       ║
║                                                      ║
║  Profile: Medistock Standard  [Switch to MARG →]    ║
╚══════════════════════════════════════════════════════╝
```

Rules:
- Shows **only shortcuts for the current scope** — not all 100+ shortcuts
- Permission-gated shortcuts marked with `[Manager+]` or `[Owner only]`
- Profile indicator at bottom with one-click switch
- ESC or Alt+F1 again to close
- Does not steal focus from current screen — overlay, not dialog

---

## 13. Mouse Parity Rules

Every keyboard shortcut action is also accessible via mouse. Full mouse support is non-negotiable for:
- First-time users
- Occasional admin tasks
- Training sessions
- Customers who don't know shortcuts yet

### Mouse-to-Shortcut Mapping

| Mouse Action | Equivalent Shortcut |
|---|---|
| Click "New Sale" button | F2 |
| Click "Search" field | F3 |
| Click "Discount" button | F4 |
| Click "Hold" button | F5 |
| Click "Pay ₹xxx" button | F6 |
| Click "Return" button | F7 |
| Click "Customer" button | F8 |
| Click "Print" button | F9 |
| Right-click any item row in cart | Context menu: Edit qty / Remove / View batch |
| Click "Save Draft" in purchase form | Ctrl+S / Ctrl+W |
| Click "Post Invoice" in purchase form | F10 |
| Hover over shortcut-enabled button | Tooltip shows shortcut: "Pay [F6]" |

### Tooltip Convention

All buttons that have a keyboard shortcut must show the shortcut in their tooltip:

```
[F6  Pay ₹194.76]     ← Button label includes shortcut hint
                       Tooltip on hover: "Complete payment and close bill (F6)"
```

This trains mouse users on shortcuts naturally without documentation.

---

## 14. Implementation: WinUI 3 KeyboardAccelerators

### Global Accelerators (App.xaml.cs)

```csharp
// Registered once at app startup from active keymap profile
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    var keymapProfile = _settingsService.GetKeymapProfile();
    _shortcutRegistry.LoadProfile(keymapProfile);
    _shortcutRegistry.RegisterGlobalAccelerators(MainWindow);
}
```

### Screen-Level Accelerators (per ViewModel)

```csharp
// PosPage.xaml.cs
protected override void OnNavigatedTo(NavigationEventArgs e)
{
    _shortcutRegistry.PushScope("pos");
    
    // Re-bind accelerators to this page
    KeyboardAccelerators.Clear();
    foreach (var binding in _shortcutRegistry.GetScopeBindings("pos"))
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = binding.Key,
            Modifiers = binding.Modifiers
        };
        accelerator.Invoked += (s, args) =>
        {
            args.Handled = true;
            binding.Command.Execute(null);
        };
        KeyboardAccelerators.Add(accelerator);
    }
}

protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
{
    _shortcutRegistry.PopScope("pos");
}
```

### Field-Level Accelerators

```csharp
// QtyField context shortcuts
QtyTextBox.KeyDown += (s, e) =>
{
    switch (e.Key)
    {
        case VirtualKey.F2:
            e.Handled = true;
            _viewModel.ViewItemTaxStatusCommand.Execute(null);
            break;
        case VirtualKey.F3:
            e.Handled = true;
            _viewModel.ChangeItemDealCommand.Execute(null);
            break;
    }
};
```

### Barcode Scanner Integration

Barcode scanners behave as HID keyboard devices — they send characters followed by `Enter`. The POS search field must be active-ready at all times:

```csharp
// POS page: always re-focus search box after any action completes
private void EnsureSearchFocused()
{
    if (!SearchBox.FocusState.HasFlag(FocusState.Keyboard))
        SearchBox.Focus(FocusState.Programmatic);
}

// Called after: item added to cart, payment completed, bill cleared
```

This means a cashier can scan the next product **immediately** after the previous one adds to cart, with zero extra keystrokes.

---

## 15. Shortcut Testing Checklist

Before every release, verify these scenarios with keyboard only (no mouse):

### POS Flow Test (Keyboard + Barcode Only)

```
[ ] F2 → New sale created, search box focused
[ ] Type product name → results appear ≤ 100ms
[ ] ↓ Arrow → navigate to product in results
[ ] Enter → product added to cart
[ ] Tab → focus on qty field
[ ] Type qty → qty updates
[ ] Enter → ready for next product
[ ] Scan barcode → product added directly to cart
[ ] F4 → discount dialog opens (if Manager role)
[ ] F8 → customer lookup opens
[ ] F6 → payment screen opens, amount field focused
[ ] Type amount → amount filled
[ ] Enter → sale completed, invoice printed
[ ] Next scan → immediately adds to new bill (no extra keypress)
[ ] F5 → bill held
[ ] Ctrl+H → held bill resumed
[ ] F7 → sale return screen opens
[ ] Escape → returns to POS cleanly
```

### Voucher Flow Test

```
[ ] Alt+V → Voucher Entry opens
[ ] F2 → Payment Voucher active
[ ] F3 → Receipt Voucher active
[ ] F4 → Journal Voucher active
[ ] F5 → Debit Note active
[ ] F6 → Credit Note active
[ ] F7 → Contra active
[ ] Ctrl+W → Saves and closes
```

### Global Test

```
[ ] Ctrl+K → Command palette opens, search works
[ ] F12 → Calculator overlay opens
[ ] Alt+F1 → Shortcut help overlay shows (scope-aware)
[ ] Ctrl+U → User change PIN prompt opens
[ ] Ctrl+\ → Sidebar collapses/expands
[ ] Tab order: logical on every form (no random jumps)
[ ] Escape: always works, never leaves user stuck
[ ] No shortcut triggers unintended action in wrong context
```

### Permission-Gated Shortcut Test

```
[ ] F4 (Discount) as Cashier with 0% limit → shows "Discount requires Manager approval" dialog
[ ] Alt+F10 (View Profit) as Cashier → silently ignored or shows permission error
[ ] Alt+F10 as Manager → opens profit view
```

---

*This document is the authoritative keyboard shortcut specification for Medistock. The Command Layer in* `ARCHITECTURE.md` *enforces that shortcuts are config over commands, not direct wiring. The Token System in* `UI_DESIGN_SYSTEM.md` *governs all visual shortcut indicators. Read all three together before implementing any keyboard interaction.*

# Medistock — UI/UX Design System & Specification
> **Version:** 1.0 | **Date:** 2026-09-24 | **Status:** Active Reference  
> **Stack:** WinUI 3 / Windows App SDK / .NET 10  
> **Scope:** Desktop-first pharmacy ERP — modern, fast, customizable

---

## TABLE OF CONTENTS
1. [Design Philosophy](#1-design-philosophy)
2. [Theme System — Dual Mode (Light + Dark)](#2-theme-system--dual-mode-light--dark)
3. [First-Launch Theme Selection Wizard](#3-first-launch-theme-selection-wizard)
4. [Color Token System](#4-color-token-system)
5. [Typography System](#5-typography-system)
6. [Spacing & Layout Grid](#6-spacing--layout-grid)
7. [Component Library](#7-component-library)
8. [App Shell & Navigation](#8-app-shell--navigation)
9. [POS Screen (Performance-Critical)](#9-pos-screen-performance-critical)
10. [Dashboard Screens (Role-Based)](#10-dashboard-screens-role-based)
11. [Data Grid & Inventory Screens](#11-data-grid--inventory-screens)
12. [Forms & Data Entry](#12-forms--data-entry)
13. [B2B Transaction Timeline UI](#13-b2b-transaction-timeline-ui)
14. [Charts & Analytics](#14-charts--analytics)
15. [User Customization Options](#15-user-customization-options)
16. [Motion & Animation Rules](#16-motion--animation-rules)
17. [Accessibility Standards](#17-accessibility-standards)
18. [Performance Budget for UI](#18-performance-budget-for-ui)
19. [WinUI 3 Implementation Notes](#19-winui-3-implementation-notes)
20. [Anti-Patterns (Never Build)](#20-anti-patterns-never-build)

---

## 1. Design Philosophy

### Core Principle
**"Operational depth of MARG + Visual clarity of a modern SaaS + Performance of a native app."**

Do **not** copy MARG's visual design. Do **not** rebuild its cluttered Windows-forms UI.  
Copy its **operational efficiency** — every action reachable in ≤ 2 keystrokes. Then make it beautiful.

### Five Design Pillars

| Pillar | What It Means |
|---|---|
| **Speed-first** | Every interaction must feel instant. No spinner where none is needed. |
| **Information density without clutter** | Show a pharmacist everything they need; hide what they don't need right now. |
| **Keyboard + Barcode Primary** | Mouse is secondary. Every core action has a hotkey. |
| **Explainability** | Show calculation breakdowns, transaction timelines, stock movement history — not just final numbers. |
| **Adaptable** | User chooses theme, density, accent color, font size. Not a one-size-fits-all skin. |

---

## 2. Theme System — Dual Mode (Light + Dark)

Both themes are **fully designed**, not one adapted from the other.  
User selects theme on first launch and can change it anytime from Settings.

### Theme Architecture (WinUI 3)

```
ApplicationTheme
    ├── Light  (default offer at first launch)
    └── Dark   (alternative)

Stored in: AppLocalSettings["AppTheme"] = "Light" | "Dark"
Applied at: Application.RequestedTheme before Window creation
Re-applied: Immediately, no restart required
```

### Design Style: "Clean Enterprise" (Both Modes)

- **Style base:** Flat Design + Data-Dense Dashboard hybrid
- **No Acrylic/Mica on data-heavy screens** (POS, inventory grids)
- **Mica** allowed only on: title bar, sidebar, dialogs
- **No animations on list insert/remove** in data views
- **Micro-animations** only on: navigation, dialogs, toasts (150–200ms)
- **Border-radius:** Cards 8px · Buttons 6px · Inputs 6px · Badges 4px · Pills 999px
- **Elevation model:** Use border + subtle shadow (never Acrylic blur on data screens)

---

## 3. First-Launch Theme Selection Wizard

On very first launch after installation, a full-screen setup wizard runs **before** any other screen.

### Wizard Step Flow

```
Step 1: Welcome + Language
Step 2: Theme Selection  ← [Light] vs [Dark] live preview
Step 3: Accent Color     ← pick from 8 preset accents
Step 4: Display Density  ← Compact / Normal / Comfortable
Step 5: Font Size        ← Small / Medium / Large
Step 6: Organization Setup (name, branch, GSTIN)
Step 7: Done → POS screen
```

### Step 2 — Theme Picker (Key Screen)

```
╔══════════════════════════════════════════════════════════╗
║          Choose Your Display Style                       ║
║                                                          ║
║   ┌───────────────────┐    ┌───────────────────┐        ║
║   │  ████████████████ │    │  ░░░░░░░░░░░░░░░░ │        ║
║   │  █ Sidebar       █│    │  ▓ Sidebar       ▓│        ║
║   │  ████████████████ │    │  ░░░░░░░░░░░░░░░░ │        ║
║   │  [Light Preview]  │    │  [Dark Preview]   │        ║
║   └───────────────────┘    └───────────────────┘        ║
║         ○ Light                  ○ Dark                  ║
║                                                          ║
║   You can change this anytime in Settings → Appearance   ║
║                                  [Continue →]            ║
╚══════════════════════════════════════════════════════════╝
```

Rules:
- Both previews show a **live mini-render** of the actual app shell, not a static image
- Clicking a preview **instantly** switches the wizard background to that theme
- No restart required. Theme is committed to `AppLocalSettings` immediately.
- "You can change this anytime" text removes decision anxiety
- Skip button available on every wizard step

---

## 4. Color Token System

All colors defined as **semantic tokens**, never hardcoded hex in components.  
Token name is the contract. Hex value changes per theme. Component code only references tokens.

### Token Architecture

```
ThemeResource (WinUI 3)
    Application.Resources
        ├── Light Theme Dictionary
        └── Dark Theme Dictionary
```

### Full Token Set

| Token Name | Light Value | Dark Value | Usage |
|---|---|---|---|
| **BACKGROUNDS** | | | |
| `AppBackground` | `#F8FAFC` | `#0D1117` | Main window background |
| `SurfaceBackground` | `#FFFFFF` | `#161B22` | Cards, panels, sidebars |
| `SurfaceElevated` | `#FFFFFF` | `#21262D` | Dialogs, popovers, dropdowns |
| `SurfaceSubtle` | `#F1F5F9` | `#0D1117` | Row alternates, section fills |
| `SidebarBackground` | `#F1F5F9` | `#13151A` | Left navigation sidebar |
| **TEXT** | | | |
| `TextPrimary` | `#0F172A` | `#E6EDF3` | Headings, primary body |
| `TextSecondary` | `#475569` | `#8B949E` | Labels, subtitles, captions |
| `TextMuted` | `#94A3B8` | `#6E7681` | Placeholders, hints |
| `TextDisabled` | `#CBD5E1` | `#3D444D` | Disabled states |
| `TextOnAccent` | `#FFFFFF` | `#FFFFFF` | Text on accent/primary buttons |
| `TextOnDanger` | `#FFFFFF` | `#FFFFFF` | Text on danger buttons |
| **ACCENT (changes per user selection)** | | | |
| `AccentPrimary` | `#15803D` | `#22C55E` | Primary buttons, active states |
| `AccentHover` | `#166534` | `#16A34A` | Hover on primary |
| `AccentSubtle` | `#DCFCE7` | `#14532D` | Accent badge fill, tag fill |
| `AccentText` | `#15803D` | `#22C55E` | Accent text on subtle bg |
| **SEMANTIC STATUS** | | | |
| `Success` | `#16A34A` | `#22C55E` | Stock OK, payment received |
| `SuccessSubtle` | `#DCFCE7` | `#14532D26` | Success badge background |
| `Warning` | `#D97706` | `#F59E0B` | Near expiry, low stock |
| `WarningSubtle` | `#FEF3C7` | `#451A0326` | Warning badge background |
| `Danger` | `#DC2626` | `#F87171` | Out of stock, errors, overdue |
| `DangerSubtle` | `#FEE2E2` | `#450A0A26` | Danger badge background |
| `Info` | `#0369A1` | `#38BDF8` | Sync status, informational |
| `InfoSubtle` | `#E0F2FE` | `#0C4A6E26` | Info badge background |
| **BORDERS & DIVIDERS** | | | |
| `BorderDefault` | `#E2E8F0` | `#30363D` | Card borders, input borders |
| `BorderFocus` | `#15803D` | `#22C55E` | Input focus ring |
| `BorderStrong` | `#CBD5E1` | `#484F58` | Dividers, table separators |
| `BorderSubtle` | `#F1F5F9` | `#21262D` | Subtle separators |
| **INTERACTIVE STATES** | | | |
| `HoverOverlay` | `rgba(0,0,0,0.04)` | `rgba(255,255,255,0.06)` | Row hover, button hover |
| `PressOverlay` | `rgba(0,0,0,0.08)` | `rgba(255,255,255,0.10)` | Button press |
| `SelectionFill` | `#DCFCE7` | `#14532D` | Selected row fill |
| `SelectionBorder` | `#15803D` | `#22C55E` | Selected row left border |
| **PHARMACY-SPECIFIC** | | | |
| `BatchExpiryCritical` | `#DC2626` | `#F87171` | Expiry < 30 days |
| `BatchExpiryWarning` | `#D97706` | `#F59E0B` | Expiry 30–90 days |
| `BatchExpiryOk` | `#16A34A` | `#22C55E` | Expiry > 90 days |
| `ScheduleHBadge` | `#7C3AED` | `#A78BFA` | Schedule H drug indicator |
| `NarcoticBadge` | `#DC2626` | `#F87171` | Narcotic/restricted flag |
| `PrescriptionRequired` | `#0369A1` | `#38BDF8` | Prescription-required flag |
| `StockLow` | `#D97706` | `#F59E0B` | Below reorder point |
| `StockOut` | `#DC2626` | `#F87171` | Zero stock |
| `SyncPending` | `#D97706` | `#F59E0B` | Outbox pending sync |
| `SyncOk` | `#16A34A` | `#22C55E` | Fully synced |

### 8 Accent Color Presets (User Selectable)

User picks their brand accent in first-launch wizard and Settings → Appearance.  
The accent drives: primary buttons, active nav indicator, focus rings, selection state.

| Name | Light Primary | Dark Primary | Character |
|---|---|---|---|
| **Pharmacy Green** *(default)* | `#15803D` | `#22C55E` | Trust, health, professional |
| **Ocean Blue** | `#0369A1` | `#38BDF8` | Corporate, calm |
| **Indigo** | `#4338CA` | `#818CF8` | Modern SaaS |
| **Teal** | `#0F766E` | `#2DD4BF` | Fresh, clinical |
| **Violet** | `#7C3AED` | `#A78BFA` | Premium |
| **Rose** | `#BE185D` | `#FB7185` | Warm |
| **Amber** | `#B45309` | `#FCD34D` | Energetic |
| **Slate** | `#334155` | `#94A3B8` | Neutral, minimal |

---

## 5. Typography System

### Selected Font Pairing: "Corporate Trust"

| Role | Font | Rationale |
|---|---|---|
| **Headings / Module Titles** | **Lexend** | Designed specifically for readability; reduces cognitive load for dense data screens |
| **Body / Data / Labels** | **Source Sans 3** | Excellent legibility at small sizes; ideal for data tables and forms |
| **Monospace (codes, invoice nos, barcodes)** | **Fira Code** | Clear differentiation for document numbers, batch codes, GSTIN |

> **Why Lexend + Source Sans 3?** Lexend was engineered for reading proficiency — it reduces visual crowding. A pharmacist reading 5,000 product names and batch codes daily benefits from this. Source Sans 3 was designed by Adobe specifically for UIs and long-form reading.

### Type Scale (WinUI 3 ResourceDictionary)

```xml
<!-- Type Scale — use ONLY these sizes, no arbitrary values -->
<x:Double x:Key="FontSize10">10</x:Double>   <!-- Badge labels, footnotes -->
<x:Double x:Key="FontSize12">12</x:Double>   <!-- Table captions, timestamps -->
<x:Double x:Key="FontSize13">13</x:Double>   <!-- Table body (compact mode) -->
<x:Double x:Key="FontSize14">14</x:Double>   <!-- Table body (normal), form labels -->
<x:Double x:Key="FontSize15">15</x:Double>   <!-- Body text, button text -->
<x:Double x:Key="FontSize16">16</x:Double>   <!-- Subheadings, card titles -->
<x:Double x:Key="FontSize18">18</x:Double>   <!-- Section headers -->
<x:Double x:Key="FontSize20">20</x:Double>   <!-- Screen titles -->
<x:Double x:Key="FontSize24">24</x:Double>   <!-- Dashboard KPI values -->
<x:Double x:Key="FontSize28">28</x:Double>   <!-- Large KPI values -->
<x:Double x:Key="FontSize32">32</x:Double>   <!-- Hero numbers (total sales, etc.) -->
```

### Font Weight Usage

| Weight | Token Name | Usage |
|---|---|---|
| 400 Regular | `FontWeightRegular` | Body text, table data, descriptions |
| 500 Medium | `FontWeightMedium` | Labels, form field labels, navigation items |
| 600 SemiBold | `FontWeightSemiBold` | Card titles, section headers, button text |
| 700 Bold | `FontWeightBold` | Screen titles, KPI values, key numbers |

### User Font Size Override (3 levels)

Applied as a global scale multiplier. Stored in settings.

| Setting | Scale | Body Size | Table Size |
|---|---|---|---|
| Small | 0.9× | 13px | 12px |
| **Medium (default)** | **1.0×** | **14px** | **13px** |
| Large | 1.15× | 16px | 14px |

---

## 6. Spacing & Layout Grid

### Spacing Scale (8dp base)

```
4px  — xs  : Inner badge padding, tight icon gaps
8px  — sm  : Component inner padding, inline gaps
12px — md  : Card inner padding (compact), form field gaps
16px — lg  : Standard card padding, section gaps
20px — xl  : Section spacing
24px — 2xl : Major section separators
32px — 3xl : Page-level padding, large section spacing
48px — 4xl : Hero sections
```

**Rule:** All spacing values must be from this scale. No arbitrary `13px`, `17px`, `22px` values.

### Density System (User Selectable)

| Setting | Table Row Height | Card Padding | Sidebar Item Height | Character |
|---|---|---|---|---|
| **Compact** | 32px | 12px | 36px | Power users, maximum data |
| **Normal** (default) | 40px | 16px | 44px | Balanced |
| **Comfortable** | 48px | 20px | 52px | New users, touchscreen |

### Window Layout Grid

```
┌─────────────────────────────────────────────────────────────┐
│  Title Bar  [App Icon] [Medistock]          [─][□][✕]       │  48px
├──────────┬──────────────────────────────────────────────────┤
│          │  Top Toolbar / Breadcrumb / Quick Actions         │  48px
│  Side    ├──────────────────────────────────────────────────┤
│  Nav     │                                                   │
│          │              Main Content Area                    │
│  240px   │              (fluid width)                        │
│  (or     │                                                   │
│  collapsed│                                                   │
│  to 56px)│                                                   │
│          │                                                   │
└──────────┴──────────────────────────────────────────────────┘
```

- Sidebar collapses to icon-only (56px) with keyboard shortcut `Ctrl+\`
- Title bar is custom-drawn (Mica effect allowed here)
- Content area max-width: **1440px** centered for very wide monitors
- Minimum supported window size: **1280×720px**

---

## 7. Component Library

### 7.1 Buttons

```
PRIMARY BUTTON         SECONDARY BUTTON       GHOST BUTTON
┌──────────────┐       ┌──────────────┐       ┌──────────────┐
│  Save Sale   │       │   Cancel     │       │   Export     │
└──────────────┘       └──────────────┘       └──────────────┘
AccentPrimary fill      BorderDefault stroke    Transparent
TextOnAccent text       TextPrimary text        AccentPrimary text
Height: 36px            Height: 36px            Height: 36px
Radius: 6px             Radius: 6px             Radius: 6px

DANGER BUTTON          ICON BUTTON             ICON + LABEL
┌──────────────┐       ┌────┐                  ┌────────────────┐
│  Cancel Bill  │       │ 🖨 │                  │ 🖨  Print      │
└──────────────┘       └────┘                  └────────────────┘
Danger fill            36×36px                 Height: 36px
TextOnDanger text      Transparent+icon        With left icon
```

**State transitions (all 150ms ease-out):**
- Default → Hover: `HoverOverlay` applied, subtle scale 0.99
- Hover → Press: `PressOverlay` applied, scale 0.97
- Any → Disabled: 40% opacity, cursor not-allowed, no pointer events
- Any → Loading: Spinner replaces content, same button dimensions

**Performance rule:** No animations on Primary/Danger buttons in the POS hot path. Only state color changes.

### 7.2 Input Fields

```
NORMAL STATE                   FOCUSED STATE
┌──────────────────────────┐   ┌──────────────────────────┐
│ Product Name         [✕] │   │ Paracetamol          [✕] │
└──────────────────────────┘   └══════════════════════════┘
  BorderDefault border           BorderFocus (AccentPrimary)
  TextMuted placeholder          2px bottom or full border
```

```
ERROR STATE                    SUCCESS STATE
┌══════════════════════════┐   ┌──────────────────────────┐
│ 0                        │   │ PCM-240923           [✓] │
└══════════════════════════┘   └──────────────────────────┘
  Danger border                  Success icon, Success border
  Error message below            
```

**Anatomy of every input field:**
1. Label (FontSize13, FontWeightMedium, TextSecondary) — above field
2. Input area (height 36px normal, 32px compact, 40px comfortable)
3. Optional leading icon (16px, TextMuted)
4. Optional trailing clear/action button
5. Optional helper text below (FontSize12, TextMuted)
6. Error message below (FontSize12, Danger) — only on validation failure

### 7.3 Data Table / Grid

```
┌─────┬──────────────────────┬────────────┬──────┬──────┬────────┐
│ ☐   │ Product Name     ▲▼  │ Batch      │ Exp  │ Qty  │ Price  │
├─────┼──────────────────────┼────────────┼──────┼──────┼────────┤
│ ☐   │ Paracetamol 500mg    │ PCM240923  │09/28 │  82  │ ₹18.00│
│ ☐   │ Amoxicillin 500mg    │ AMX240812  │03/27 │   5  │ ₹42.00│◄ Low stock
│ ☑   │ Pantoprazole 40mg    │ PNT231201  │12/24 │   0  │ ₹32.00│◄ Expired!
└─────┴──────────────────────┴────────────┴──────┴──────┴────────┘
```

Rules:
- **Virtualization required** for all tables > 50 rows (`ItemsRepeater` with `UniformGridLayout`)
- **Flat row template** — no nested Grid > 2 levels deep
- Row height matches density setting (32/40/48px)
- Column headers: sortable, sticky, 1px bottom border
- **Row selection:** Left border `SelectionBorder` (3px) + `SelectionFill` background
- **Row hover:** `HoverOverlay` — color shift only, no layout change
- **Alternating rows:** Disabled by default; optional in Settings (some users prefer it)
- **Right-click context menu:** Standard actions for that row type
- **Inline status badges:** Expiry/Stock status use compact color pills (no icons in table — icon-free in row template for performance)
- **Column resize:** All columns user-resizable; widths persisted per screen in settings
- **Sticky first column** for tables with > 6 columns

### 7.4 Status Badges

Compact, non-interactive color pills for quick scanning:

```
[● In Stock]      [● Low Stock]      [● Out of Stock]
[● Exp: 12/24]    [● Near Expiry]    [● Expired]
[● Sch-H]         [● Narcotic]       [● Rx Req]
[● Synced]        [● Pending Sync]   [● Draft]
[● PAID]          [● PARTIAL]        [● OVERDUE]
```

Anatomy: 4px radius · 4px vertical padding · 8px horizontal padding · FontSize11 · FontWeightMedium

### 7.5 KPI Cards (Dashboard)

```
┌────────────────────────────┐
│ Today's Sales         📈   │
│                            │
│  ₹1,24,500                 │
│                            │
│  ↑ 12% vs yesterday        │
└────────────────────────────┘
```

- Border: `BorderDefault`
- Background: `SurfaceBackground`
- Padding: 16px (normal) / 12px (compact)
- Title: FontSize13, FontWeightMedium, TextSecondary
- Value: FontSize28 or 32, FontWeightBold, TextPrimary
- Trend: FontSize12, color = Success/Danger based on direction
- Icon: 20px, TextMuted, top-right corner

### 7.6 Toast Notifications

```
Position: Bottom-right corner, 16px from edge
Stack: Up to 3 toasts; older ones push up and fade
Duration: Info/Success = 4s; Warning = 6s; Error = persist until dismissed

[✓] Sale saved — INV-2026-000182    [×]    ← Success (green left bar)
[⚠] Low stock: Amoxicillin (5 left) [×]    ← Warning (amber left bar)  
[✕] Sync failed — will retry        [×]    ← Error (red left bar)
[ℹ] Paracetamol near expiry (29d)   [×]    ← Info (blue left bar)
```

- Width: 320px
- Enter animation: slide-in-right, 200ms ease-out
- Exit animation: slide-out-right, 150ms ease-in
- **Never block POS workspace** — positioned away from cart area

### 7.7 Dialogs & Bottom Sheets

```
CONFIRMATION DIALOG
┌───────────────────────────────┐
│  Cancel Bill?                 │
│                               │
│  INV-DRAFT-00023 will be      │
│  permanently discarded.       │
│                               │
│  [Keep Bill]   [Cancel Bill]  │
└───────────────────────────────┘
Max width: 440px
Overlay: rgba(0,0,0,0.5) — dark mode too
Radius: 12px
Animation: fade-in + scale from 0.95 → 1.0, 180ms
```

- **ESC always dismisses** (unless destructive action pending)
- **Primary action is always right** (Cancel Bill = danger button, right)
- **Destructive actions are red**, never green
- **Max one primary action per dialog**

---

## 8. App Shell & Navigation

### Sidebar Navigation (Left)

```
EXPANDED (240px)                    COLLAPSED (56px, icon only)
┌────────────────────┐              ┌──────┐
│ ⬡ Medistock        │              │  ⬡   │
├────────────────────┤              ├──────┤
│ 🔍  Quick search   │              │  🔍  │
│     Ctrl+K         │              │      │
├────────────────────┤              ├──────┤
│ ██ POS             │              │  ██  │ ← Active (AccentPrimary left bar)
│    Inventory       │              │  📦  │
│    Purchase        │              │  🛒  │
│    Sales History   │              │  📋  │
│    Customers       │              │  👥  │
│    Suppliers       │              │  🏭  │
│    Accounting      │              │  📊  │
│    B2B Network     │              │  🔗  │
│    Reports         │              │  📈  │
│    Settings        │              │  ⚙   │
├────────────────────┤              ├──────┤
│ [●] Synced  [💡]   │              │  [●] │ ← Sync status + theme toggle
│ Rahul · Cashier    │              │  👤  │
└────────────────────┘              └──────┘
```

Active indicator: 3px AccentPrimary left border + AccentSubtle row fill  
Hover: HoverOverlay fill only  
Icon size: 18px, consistent weight (outline style default, filled when active)  
Sidebar collapse: `Ctrl+\` — animates in 150ms ease-out; setting persisted

### Top Toolbar (per screen)

```
┌──────────────────────────────────────────────────────────┐
│ POS → Active Bill                  [🔄 Sync ●] [🔔3] [?] │
└──────────────────────────────────────────────────────────┘
```

- Breadcrumb / screen title (left)
- Screen-specific quick actions (context-aware, right of title)
- Global status indicators (right): Sync status dot · Notification bell · Help

### Command Palette (`Ctrl+K`)

```
┌─────────────────────────────────────────────────────────┐
│  🔍  Search anything...                                  │
├─────────────────────────────────────────────────────────┤
│  RECENT                                                  │
│  📋  INV-2026-000182 · Sale · ₹2,400                    │
│  👤  Rahul Kumar · Customer                              │
│  📦  Paracetamol 500mg · 82 in stock                    │
├─────────────────────────────────────────────────────────┤
│  ACTIONS                                                 │
│  ⚡  New Sale                                  [F2]      │
│  🛒  New Purchase                              [F10]     │
│  📊  Open Sales Report                                   │
│  ⚙   Stock Adjustment                                    │
└─────────────────────────────────────────────────────────┘
Width: 600px · Max height: 480px (scrollable)
Opens: fade-in + blur overlay, 120ms
ESC to dismiss
```

---

## 9. POS Screen (Performance-Critical)

> ⚠️ **Performance Override Rules apply here.** No Mica. No animations on item add/remove. No decorative effects. Flat rendering only. This screen must stay sub-100ms for every interaction.

### Layout

```
┌──────────────────────────────────────────────────────────────┐
│ POS · Counter 1 · Rahul          [F5 Hold] [F7 Return] [F8?] │
├──────────────────────────────┬───────────────────────────────┤
│                              │  BILL SUMMARY                 │
│  🔍 Search or scan...        │  Customer: Walk-in Customer   │
│  [Product search box]        │  Bill: INV-DRAFT-0023         │
│                              │                               │
│  SEARCH RESULTS              │  Paracetamol 500mg ×2  ₹36   │
│  ─────────────────           │  Amoxicillin 500mg ×1  ₹42   │
│  Paracetamol 500mg  ₹18      │  Pantoprazole 40mg ×3  ₹96   │
│  [82] · Batch PCM240923      │  ─────────────────────────── │
│  Paracetamol 650mg  ₹22      │  Subtotal           ₹174.00  │
│  [45] · Batch PCM240801      │  GST (12%)           ₹20.88  │
│  Para Syrup 120ml   ₹48      │  Discount (F4)        ₹0.00  │
│  [12] · Batch SYP240515      │  Round off           -₹0.12  │
│                              │  ─────────────────────────── │
│                              │  TOTAL              ₹194.76  │
│                              │                               │
│                              │  [F6 PAY ₹194.76]            │
└──────────────────────────────┴───────────────────────────────┘
```

### POS-Specific Design Rules

1. **Two-panel split** — Search/results left, running bill right
2. **No card containers** in search results — flat list rows only (performance)
3. **No images** in search results — text only in the dropdown (images block search latency)
4. **Results list:** Flat `ItemsRepeater`, virtualized, rows 40px height
5. **Every scanned/clicked item animates into bill** — only the number counter animates (+1), not the row insertion
6. **Bill total and F6 button** always visible, always in viewport — never scroll off
7. **F6 Pay button** is the largest element in the bill panel — full-width, 48px height, AccentPrimary
8. **Keyboard-only workflow** is the primary — barcode scan → quantity focus → Tab → scan next
9. **Color coding in bill items:** Normal = TextPrimary; Expired batch = Danger text; Low stock = Warning text
10. **Schedule-H indicator** on product in search results: small purple `[Sch-H]` badge, non-blocking

### Payment Screen (F6)

```
┌──────────────────────────────────────────────┐
│  Payment — ₹194.76                           │
├──────────────────────────────────────────────┤
│  Payment Mode                                │
│  [CASH]  [CARD]  [UPI]  [CREDIT]  [SPLIT]   │
│                                              │
│  Amount Received  ₹ [200.00     ]           │
│  Change           ₹ 5.24                    │
│                                              │
│  [← Back]              [✓ Complete Sale F6] │
└──────────────────────────────────────────────┘
```

- Large number display for "Change" (FontSize28, bold) — critical for cashier
- Amount received field auto-focuses on dialog open
- Number pad shortcut: typing digits fills the amount field directly
- Tab cycles: amount → complete sale
- Complete Sale `F6` or `Enter` — one keystroke to finalize

---

## 10. Dashboard Screens (Role-Based)

### Owner Dashboard

```
┌──────────────┬──────────────┬──────────────┬──────────────┐
│ Today Sales  │ Gross Profit │ Outstanding  │ Expiry Risk  │
│ ₹1,24,500   │ 18.2%        │ ₹3,20,000   │ ₹87,500     │
│ ↑12% vs yst │ ↓1.2%        │ ●Overdue ₹12k│ ⚠ 60d band  │
└──────────────┴──────────────┴──────────────┴──────────────┘
┌──────────────────────────┬───────────────────────────────┐
│  Sales Trend (7 days)    │  Top Products (Today)         │
│  [Area chart]            │  [Horizontal bar chart]       │
│                          │                               │
└──────────────────────────┴───────────────────────────────┘
┌──────────────────────────┬───────────────────────────────┐
│  Stock Alerts            │  Pending B2B Orders           │
│  ⚠ 8 low stock items     │  3 orders awaiting receipt    │
│  🔴 2 out of stock        │  1 order packed, pick today   │
│  [View all →]            │  [View all →]                 │
└──────────────────────────┴───────────────────────────────┘
```

### Pharmacist Dashboard

Focus: prescriptions, expiry, drug schedule alerts

```
┌──────────────┬──────────────┬──────────────┬──────────────┐
│ Pending Rx   │ Near Expiry  │ Out of Stock │ Sch-H Sales  │
│ 4 items      │ 12 batches   │ 2 products   │ Today: 8     │
└──────────────┴──────────────┴──────────────┴──────────────┘
[Expiry Timeline — horizontal strip with 30/60/90d color bands]
[Prescription Queue — patient, doctor, status]
```

---

## 11. Data Grid & Inventory Screens

### Inventory Grid — Performance Rules

```
HEADER ROW (sticky, 40px)
┌─────┬──────────────────────┬────────────┬──────┬──────┬──────┬───────┐
│ ☐   │ Product              │ Batch      │ Exp  │ Avail│ Resv │ Value │
├─────┼──────────────────────┼────────────┼──────┼──────┼──────┼───────┤
│     │                      │            │      │      │      │       │
│     │   ItemsRepeater      │            │      │      │      │       │
│     │   Virtual rows       │            │      │      │      │       │
│     │   5,000+ rows        │            │      │      │      │       │
│     │   60fps scroll       │            │      │      │      │       │
│     │                      │            │      │      │      │       │
└─────┴──────────────────────┴────────────┴──────┴──────┴──────┴───────┘
FOOTER: [Showing 1-50 of 5,284 items]  [< 1 2 3 ... >]  [Export]
```

Rules enforced:
- `ItemsRepeater` with `UniformGridLayout` — no `DataGrid` (too heavy)
- Row template: single flat `Grid` with 1 level nesting max
- No images in default view
- No animations on scroll, row add, or row remove
- Filter panel: slide-in from right (200ms), not inline — keeps grid full-width
- Column sorting triggers spinner only if query takes > 200ms (otherwise instant)
- Bulk select: checkbox column → action bar slides in from bottom with selected count + actions

### Filter Panel (Slide-In Right)

```
← [Filter Inventory]                              [Reset] [Apply]
────────────────────────────────────────────────────────────────
Category          [All categories ▼]
Manufacturer      [All ▼]
Expiry Range      [○ All  ○ <30d  ○ 30–90d  ○ >90d]
Stock Status      [○ All  ○ Low  ○ Out  ○ OK]
Schedule          [○ All  ○ OTC  ○ Sch-H  ○ Narcotic]
Branch/Warehouse  [Branch A ▼]
────────────────────────────────────────────────────────────────
Active filters: [Category: Antibiotics ×] [Stock: Low ×]
```

---

## 12. Forms & Data Entry

### Purchase Entry Form — Screen Layout

```
┌─────────────────────────────────────────────────────────┐
│ New Purchase Entry                         [Save Draft] │
├────────────────────────┬────────────────────────────────┤
│  Supplier              │  Invoice Details               │
│  [ABC Pharma ▼]        │  Invoice No  [    ]            │
│  GSTIN: 27AAACM...     │  Invoice Date [24/09/2026]     │
│  Credit: ₹1,40,000/₹2L│  Due Date    [24/10/2026]      │
├────────────────────────┴────────────────────────────────┤
│  Items                                          [+ Add] │
│  ┌──────┬──────┬──────┬──────┬──────┬──────┬──────────┐│
│  │Prod  │Batch │Exp   │Qty   │Rate  │GST%  │Amount    ││
│  ├──────┼──────┼──────┼──────┼──────┼──────┼──────────┤│
│  │[Sel▼]│[    ]│[    ]│[    ]│[    ]│ 12%  │₹0.00     ││
│  └──────┴──────┴──────┴──────┴──────┴──────┴──────────┘│
├─────────────────────────────────────────────────────────┤
│                    Subtotal  ₹0.00                      │
│                    GST       ₹0.00                      │
│                    Total     ₹0.00                      │
│                              [Save Draft] [Post Invoice] │
└─────────────────────────────────────────────────────────┘
```

Form UX Rules:
- Tab key moves through fields in logical data-entry order
- Barcode scan in the product field fills product automatically
- After product selection: cursor auto-moves to Batch field
- After batch: cursor auto-moves to Expiry
- After Expiry: cursor auto-moves to Qty
- Qty validation is instant (no submit needed to see errors)
- GST% is auto-calculated and read-only (derived from product HSN)
- Amount is auto-calculated and read-only
- [Post Invoice] requires confirmation dialog if total > ₹50,000

---

## 13. B2B Transaction Timeline UI

Every B2B transaction shows a visual timeline:

```
B2B ORDER #B2B-2026-000182                          [RECEIVED ✓]
Pharmacy → ABC Pharma Wholesaler

──────────────────────────────────────────────────────────────
  ✓  10:02   Order submitted by Rahul
  ✓  10:07   Accepted by wholesaler (80 of 100 Paracetamol)
  ✓  10:25   Items picked from warehouse
  ✓  10:41   Package PKG-00091 packed
  ✓  10:44   Ready for pickup — notified
  ✓  11:03   Collected by Rahul (OTP verified)
  ✓  11:04   Sales Invoice SI-00891 issued (₹3,540)
  ✓  11:18   Goods received — all items match
  ✓  11:19   Purchase Invoice PI-00421 created
  ✓  11:20   Transaction completed
──────────────────────────────────────────────────────────────
Items: Paracetamol 80×₹18  Amoxicillin 50×₹42
Total: ₹3,540   GST: ₹424.80   Payment: UNPAID [Pay Now →]
```

Timeline dot colors:
- ✓ Completed: `Success` color
- ● Current step: `AccentPrimary` pulsing dot (1.5s pulse, stops when complete)
- ○ Pending: `TextMuted` hollow dot
- ✕ Failed/Rejected: `Danger` color

---

## 14. Charts & Analytics

### Chart Types Used (per screen)

| Screen | Chart Type | Library (WinUI 3) |
|---|---|---|
| Dashboard sales trend | **Area chart** (7/30/90 day) | LiveCharts2 or SkiaSharp |
| Dashboard top products | **Horizontal bar chart** | LiveCharts2 |
| Expiry dashboard | **Stacked bar by time band** | LiveCharts2 |
| Stock value breakdown | **Donut chart** (by category) | LiveCharts2 |
| GST summary | **Grouped bar** (CGST/SGST/IGST) | LiveCharts2 |
| Sales by payment mode | **Donut chart** | LiveCharts2 |
| Cashier performance | **Bullet chart** (vs target) | Custom SVG |
| Daily cash register | **Line chart** (hour by hour) | LiveCharts2 |

### Chart Performance Rules

- Charts render **off the UI thread** via SkiaSharp canvas
- Charts **never block** page load — load with skeleton placeholder, fill when data is ready
- **No chart animations** on the Reports screen (where large datasets are expected)
- Subtle **fade-in animation** (200ms) on dashboard charts only, when data first loads
- Hover tooltips: plain text tooltip, no fancy animations
- All charts have a **"View Table" fallback** — accessible text representation

### Chart Color Palette (Consistent across all charts)

```
Series 1:  #15803D (or current AccentPrimary)
Series 2:  #0369A1  (Info blue)
Series 3:  #D97706  (Warning amber)
Series 4:  #7C3AED  (Violet)
Series 5:  #0F766E  (Teal)
Series 6:  #BE185D  (Rose)
Danger:    #DC2626
Neutral:   #94A3B8
```

Same colors in both light and dark mode (sufficient contrast in both).

---

## 15. User Customization Options

Available at: **Settings → Appearance**

### All Customizable Options

| Option | Choices | Default | Stored In |
|---|---|---|---|
| **Theme** | Light / Dark | (selected at first launch) | AppLocalSettings |
| **Accent Color** | 8 presets | Pharmacy Green | AppLocalSettings |
| **Display Density** | Compact / Normal / Comfortable | Normal | AppLocalSettings |
| **Font Size** | Small / Medium / Large | Medium | AppLocalSettings |
| **Sidebar** | Expanded / Collapsed | Expanded | AppLocalSettings |
| **Alternating Row Colors** | On / Off | Off | AppLocalSettings |
| **Show Product Images in Search** | On / Off | Off | AppLocalSettings |
| **Animations** | Enabled / Reduced / Off | Enabled | AppLocalSettings |
| **POS Layout** | Split (default) / Full Search / Full Bill | Split | AppLocalSettings |
| **Default Dashboard Tab** | (any role-dashboard) | Owner | AppLocalSettings |
| **Number Format** | ₹1,00,000 (Indian) / ₹100,000 (Global) | Indian | AppLocalSettings |
| **Date Format** | DD/MM/YYYY / MM/DD/YYYY / YYYY-MM-DD | DD/MM/YYYY | AppLocalSettings |

### Appearance Settings Screen Layout

```
Settings → Appearance
─────────────────────────────────────────────────
Theme
  [○ Light]  [● Dark]      ← Live preview mini card
  Preview: [live mini-render of app shell]

Accent Color
  [●Green][○Blue][○Indigo][○Teal][○Violet][○Rose][○Amber][○Slate]

Display Density
  [○ Compact]  [● Normal]  [○ Comfortable]
  Preview: [3-row mini table showing row heights]

Font Size
  [○ Small]  [● Medium]  [○ Large]
  Preview: [Sample text at selected size]

Advanced
  [☑] Alternating row colors in data tables
  [☐] Show product images in search results
  [●] Animations: Enabled  [○ Reduced]  [○ Off]
─────────────────────────────────────────────────
Changes apply instantly — no restart required
```

**All settings apply instantly** (no save button, no restart). This is critical for trust — user sees immediate feedback that their selection worked.

---

## 16. Motion & Animation Rules

### Animation Budget by Screen Type

| Screen | Animation Budget | Rule |
|---|---|---|
| POS / Barcode scan | **Zero** | No transitions in the hot path |
| Inventory grid | **Zero on list** | No insert/remove animations |
| Navigation (sidebar click) | 150ms ease-out | Content cross-fade only |
| Dialog open/close | 180ms (open) / 130ms (close) | Scale + fade |
| Toast notifications | 200ms slide-in / 150ms slide-out | Translate + fade |
| Dashboard charts (first load) | 200ms fade-in | After data ready |
| Command palette | 120ms fade-in | Backdrop + content |
| Page transitions | 150ms fade | No slide — slides feel slow on wide screens |
| Sidebar expand/collapse | 150ms ease-out | Width animate |
| Dropdown menus | 100ms fade + slight scale | Near-instant |

### Easing Curves

```
Default:   cubic-bezier(0.4, 0, 0.2, 1)   — Material standard ease
Enter:     cubic-bezier(0, 0, 0.2, 1)      — Decelerate (from fast to slow)
Exit:      cubic-bezier(0.4, 0, 1, 1)      — Accelerate (exits faster than enters)
Spring:    WinUI RepositionThemeAnimation   — For repositioning elements
```

### Reduced Motion

When user selects "Animations: Reduced" or "Animations: Off" in Settings, OR when system accessibility setting `prefers-reduced-motion` is active:
- All transitions become instant (0ms) or simple opacity fade only
- No scale transforms
- No slide animations
- Toasts still show/hide but without slide (just fade)

---

## 17. Accessibility Standards

### Target: WCAG 2.1 AA (WCAG AAA where feasible)

| Area | Requirement | Implementation |
|---|---|---|
| **Color contrast (text)** | ≥ 4.5:1 for body / ≥ 3:1 for large text | Token system guarantees this in both themes |
| **Color contrast (UI components)** | ≥ 3:1 for borders and interactive elements | BorderDefault and BorderFocus meet this |
| **Focus visibility** | Visible keyboard focus ring on all interactive elements | 2px `AccentPrimary` ring, 2px offset |
| **Color not sole indicator** | Never use color alone to convey meaning | All status badges have text label + color |
| **Touch/click targets** | ≥ 44×44px for all interactive elements | Buttons 36px height + 8px padding = 44px |
| **Screen reader** | All interactive elements labeled | `AutomationProperties.Name` on all controls |
| **Keyboard navigation** | Full app navigable via keyboard only | Tab order matches visual order |
| **Reduced motion** | Respect system preference | Checked in app startup and Settings |
| **Font scaling** | UI must not break at Large font size | Tested at all 3 font sizes |
| **High contrast** | WinUI 3 high-contrast mode supported | Use system brushes where possible |

---

## 18. Performance Budget for UI

### Rendering Rules (from TRD — Non-Negotiable)

| Rule | Constraint |
|---|---|
| POS search results render | ≤ 16ms per frame (60fps) |
| Inventory grid scroll | 60fps, no dropped frames at 5,000+ rows |
| Dialog open | ≤ 180ms from click to visible |
| Page navigation | ≤ 150ms from click to first paint |
| Toast appear | ≤ 100ms from event to notification shown |

### WinUI 3 Performance Constraints

```
✅ Use:  ItemsRepeater + UniformGridLayout/StackLayout (built-in virtualization)
✅ Use:  x:Bind (compiled bindings) on ALL hot-path screens
✅ Use:  x:Load deferred on off-screen panels
✅ Use:  Flat visual trees (max 3 levels in repeated row templates)
✅ Use:  Async image loading with placeholders

❌ Ban:  Acrylic/Mica on POS, inventory grid, or any data-dense screen
❌ Ban:  Animations on list insert/remove in data views
❌ Ban:  Nested Grid > 2 levels inside repeated row templates
❌ Ban:  {Binding} (reflection-based) on hot-path screens — only x:Bind
❌ Ban:  Synchronous I/O calls on UI thread (await everything)
❌ Ban:  Images in default search results row
❌ Ban:  Heavy shadows (box-shadow with blur > 8px) on scrollable lists
```

### Image Handling

- Product images: compressed at ingestion time (thumbnail 80×80px + full 400×400px)
- Thumbnails loaded lazily — only when row is in viewport and stays 200ms+
- Images never loaded eagerly for search results
- Image placeholder: `SurfaceSubtle` colored rectangle, same dimensions

---

## 19. WinUI 3 Implementation Notes

### ResourceDictionary Structure

```
App.xaml
  ├── Tokens.xaml           (all color tokens, both themes)
  ├── Typography.xaml       (type scale, font weights)
  ├── Spacing.xaml          (spacing constants)
  ├── Components/
  │   ├── Buttons.xaml
  │   ├── Inputs.xaml
  │   ├── Tables.xaml
  │   ├── Badges.xaml
  │   ├── Cards.xaml
  │   ├── Dialogs.xaml
  │   └── Toasts.xaml
  └── Animations.xaml       (all storyboards + easing)
```

### Theme Switching (Runtime, No Restart)

```csharp
// In ThemeService.cs
public void SetTheme(AppTheme theme)
{
    var frame = Window.Current.Content as Frame;
    if (frame?.RequestedTheme != (ElementTheme)theme)
    {
        frame.RequestedTheme = (ElementTheme)theme;
        AppSettings.Set("AppTheme", theme.ToString());
    }
}
```

### Compiled Bindings on Hot Screens (Required)

```xml
<!-- POS screen — ALL bindings must be x:Bind -->
<TextBlock Text="{x:Bind Product.Name, Mode=OneWay}" />
<TextBlock Text="{x:Bind Product.AvailableQty, Mode=OneWay}" />

<!-- NEVER on hot screens: -->
<TextBlock Text="{Binding Product.Name}" />  <!-- Reflection-based — BANNED on POS -->
```

### ItemsRepeater for All Large Lists

```xml
<ScrollViewer>
  <ItemsRepeater ItemsSource="{x:Bind SearchResults}"
                 Layout="{StaticResource StackLayout}">
    <ItemsRepeater.ItemTemplate>
      <DataTemplate x:DataType="models:ProductSearchResult">
        <!-- FLAT template — no nested Grids -->
        <Grid Height="40" Padding="8,0" ColumnDefinitions="*,Auto,Auto">
          <TextBlock Grid.Column="0" Text="{x:Bind Name}" />
          <TextBlock Grid.Column="1" Text="{x:Bind AvailableQty}" />
          <TextBlock Grid.Column="2" Text="{x:Bind Price}" />
        </Grid>
      </DataTemplate>
    </ItemsRepeater.ItemTemplate>
  </ItemsRepeater>
</ScrollViewer>
```

---

## 20. Anti-Patterns (Never Build)

| Anti-Pattern | Why Banned |
|---|---|
| Acrylic/Mica blur on POS or inventory grid | GPU compositor cost on integrated graphics; causes stutter |
| Animations on list row insert/remove | First visible lag point on 4GB RAM machines |
| DataGrid for 5,000+ rows without virtualization | Memory explosion; 60fps scroll impossible |
| `{Binding}` on any POS/inventory/search screen | Reflection overhead per frame, per row |
| Product images in search result rows | Adds layout + network cost; search must stay ≤100ms |
| Synchronous DB call on UI thread | Single blocking call = visible freeze on HDD machines |
| Cloud API call in search/barcode path | If internet is slow/down, POS breaks |
| Only color to indicate status (no text label) | Colorblind users cannot distinguish states |
| Hardcoded hex colors in component code | Breaks theme switching; must use tokens only |
| Storing theme/settings in cloud sync | Settings are local/device-specific only |
| Full-page loading spinner on search results | Feels slow; search should update in-place instantly |
| Modal dialog for every small action | High-friction; use inline validation + confirmation only for destructive actions |
| Gradient backgrounds on data-dense screens | Visual noise; reduces readability of data |
| Custom scrollbars that don't feel native | Breaks WinUI 3 native scroll physics |
| Toast notifications covering the POS workspace | Cashier loses context during sale |

---

*This UI specification is the authoritative design reference. Every screen built for Medistock must comply with the token system, performance rules, and component patterns defined here. Deviation requires explicit architectural review.*

# Medistock — Product Requirements Document (PRD)
> **Version:** 1.0 | **Date:** 2026-09-24 | **Status:** Active — Phase 0  
> **Product:** Medistock Pharmacy ERP Platform  
> **Audience:** Developers, QA, Designers, Stakeholders  
> **Cross-references:** `ARCHITECTURE.md` · `UI_DESIGN_SYSTEM.md` · `KEYBOARD_SHORTCUTS.md` · `trd.md`

---

## DOCUMENT CONVENTIONS

- **REQ-XXX-NNN** — Functional requirement ID (e.g. REQ-POS-001)
- **NFR-XXX-NNN** — Non-functional requirement ID (e.g. NFR-PERF-001)
- **AC:** — Acceptance Criteria (must pass for feature to be marked done)
- 🔴 **P1 Critical** — Must ship in Phase 1; system is unusable without it
- 🟡 **P2 High** — Must ship in its designated phase; core workflow gap if missing
- 🟢 **P3 Normal** — Required but not blocking earlier phase completion
- 🔵 **P4 Enhancement** — Valuable; can slip to next patch if schedule is tight

---

## TABLE OF CONTENTS

1. [Product Overview](#1-product-overview)
2. [User Roles & Personas](#2-user-roles--personas)
3. [Platform & First-Launch Requirements](#3-platform--first-launch-requirements)
4. [Organisation & Branch Management](#4-organisation--branch-management)
5. [Identity, Access & Security](#5-identity-access--security)
6. [Product Master](#6-product-master)
7. [Inventory Management](#7-inventory-management)
8. [Point of Sale (POS)](#8-point-of-sale-pos)
9. [Sales History & Returns](#9-sales-history--returns)
10. [Purchase & Procurement](#10-purchase--procurement)
11. [Accounting Engine](#11-accounting-engine)
12. [GST & Compliance](#12-gst--compliance)
13. [Suppliers & Accounts Payable](#13-suppliers--accounts-payable)
14. [Customers, Patients & CRM](#14-customers-patients--crm)
15. [B2B Commerce Network](#15-b2b-commerce-network)
16. [Multi-Branch Management](#16-multi-branch-management)
17. [Reports & Analytics](#17-reports--analytics)
18. [Notifications & Alerts](#18-notifications--alerts)
19. [Settings & Configuration](#19-settings--configuration)
20. [Offline Sync & Connectivity](#20-offline-sync--connectivity)
21. [Hardware Integration](#21-hardware-integration)
22. [Cloud Management Dashboard](#22-cloud-management-dashboard)
23. [AI & Intelligence Layer (Phase 9)](#23-ai--intelligence-layer-phase-9)
24. [Platform — Update, Audit & Data](#24-platform--update-audit--data)
25. [Non-Functional Requirements](#25-non-functional-requirements)
26. [Out of Scope](#26-out-of-scope)

---

## 1. Product Overview

### 1.1 Product Mission

Medistock is an **offline-first, multi-branch pharmacy operating system** that combines:
- Store ERP (POS, Inventory, Purchase, Sales, Accounting)
- Pharmacy Operations (Batch, Expiry, Prescription, Patient, Doctor, Drug Schedule)
- B2B Commerce Network (Pharmacy ↔ Wholesaler ↔ Distributor)
- Management Cloud (Multi-branch, Analytics, Reports, Users)
- Platform (Identity, Security, Sync, Audit, Updates)

### 1.2 Target Users

| User Type | Daily Volume | Primary Need |
|---|---|---|
| Retail Pharmacist / Cashier | 50–500 sales/day | Fast, reliable POS |
| Pharmacy Owner | Daily check | Revenue, stock health, compliance |
| Store Manager | Daily | Operations oversight |
| Pharmacist (clinical) | Ongoing | Prescription gate, drug safety |
| Accountant | Daily | Books, GST, payables, receivables |
| Storekeeper | Daily | GRN, stock management, expiry |
| Procurement staff | Weekly | Purchase orders, supplier management |
| Wholesaler/Distributor | B2B | Receive orders, pick, pack, deliver |
| Multi-branch Owner | Cloud | Consolidated view across all branches |

### 1.3 Baseline Deployment Hardware

The system must run acceptably on the **minimum pharmacy rig**:
- **CPU:** Dual-core Intel/AMD 2.0GHz (integrated graphics)
- **RAM:** 4 GB DDR4
- **Storage:** 500 GB HDD or SATA SSD
- **OS:** Windows 10 (21H2) or Windows 11
- **LAN:** 100 Mbps ethernet between workstations and local server
- **Internet:** 10 Mbps broadband (unreliable — may go down anytime)

All performance targets in this document are measured on this rig, not a developer's machine.

### 1.4 Guiding Principles (Non-Negotiable)

1. **Offline-first:** Pharmacy must continue operating when internet is down, or even when local server is temporarily down.
2. **No data loss:** Every transaction is committed locally before any network operation. Outbox pattern ensures sync eventually completes.
3. **Keyboard-first:** A trained pharmacist should be able to run an entire sale day without touching the mouse.
4. **Financial immutability:** Posted invoices, journal entries, and payments are never silently modified. Corrections use reversal/credit note mechanisms.
5. **Pharmacy safety:** Drug schedule gates (H, H1, Narcotic), expiry hard blocks, and prescription enforcement are non-optional safety features.
6. **Tenant isolation:** One organisation's data is never accessible to another. `org_id` is always derived from authenticated JWT, never trusted from client payload.

---

## 2. User Roles & Personas

### 2.1 Role Definitions

| Role Code | Role Name | Description |
|---|---|---|
| `SUPER_ADMIN` | Super Admin | Medistock platform admin — only for system-level ops |
| `ORG_OWNER` | Organisation Owner | Full access to their organisation across all branches |
| `BRANCH_MANAGER` | Branch Manager | Full access to their assigned branch(es) |
| `PHARMACIST` | Pharmacist | Clinical role — prescriptions, Schedule-H, dispensing |
| `CASHIER` | Cashier | POS only — sell, hold bill, accept payment |
| `ACCOUNTANT` | Accountant | Accounting, GST, reconciliation, reports |
| `STOREKEEPER` | Storekeeper | GRN, stock adjustment, transfer, expiry |
| `PROCUREMENT` | Procurement Staff | Purchase orders, supplier management |
| `DELIVERY` | Delivery Agent | B2B delivery and pickup operations only |
| `WHOLESALER_ADMIN` | Wholesaler Admin | B2B seller-side — orders, picking, invoicing |
| `WHOLESALER_PICKER` | Warehouse Picker | Pick and pack B2B orders |
| `REPORT_VIEWER` | Report Viewer | Read-only access to selected reports |

### 2.2 Role Hierarchy

```
ORG_OWNER
  └─ BRANCH_MANAGER (per branch)
       ├─ PHARMACIST
       ├─ CASHIER
       ├─ ACCOUNTANT
       ├─ STOREKEEPER
       └─ PROCUREMENT
```

Roles are **not hardcoded** — permissions are assigned to roles dynamically via the Permission system. The table above is the default suggested mapping, not a rigid hierarchy.

### 2.3 Permission Codes (Selected Critical)

| Permission Code | Description | Default Role(s) |
|---|---|---|
| `SALE_CREATE` | Create a new sale | CASHIER, PHARMACIST, BRANCH_MANAGER |
| `SALE_RETURN` | Process sale return | BRANCH_MANAGER, PHARMACIST |
| `SALE_CANCEL_POSTED` | Cancel a posted invoice | BRANCH_MANAGER, ORG_OWNER |
| `DISCOUNT_APPLY_ITEM` | Apply item-level discount | CASHIER (≤5%), BRANCH_MANAGER (≤20%), ORG_OWNER (unlimited) |
| `DISCOUNT_APPLY_BILL` | Apply bill-level discount | CASHIER (≤5%), BRANCH_MANAGER (≤20%), ORG_OWNER (unlimited) |
| `PRICE_OVERRIDE` | Override MRP or sale rate | BRANCH_MANAGER, ORG_OWNER |
| `PROFIT_VIEW` | View profit margin on bill | BRANCH_MANAGER, ORG_OWNER |
| `PURCHASE_CREATE` | Create purchase invoice | STOREKEEPER, PROCUREMENT |
| `PURCHASE_POST` | Post/finalise purchase invoice | BRANCH_MANAGER, ACCOUNTANT |
| `STOCK_ADJUST` | Manual stock adjustment | STOREKEEPER, BRANCH_MANAGER |
| `STOCK_TRANSFER` | Initiate inter-branch transfer | STOREKEEPER, BRANCH_MANAGER |
| `JOURNAL_POST` | Post accounting journal entry | ACCOUNTANT, BRANCH_MANAGER |
| `JOURNAL_REVERSE` | Reverse a posted journal | ACCOUNTANT, ORG_OWNER |
| `GST_FILE` | File GST returns | ACCOUNTANT, ORG_OWNER |
| `B2B_ORDER_CREATE` | Create B2B purchase request | PROCUREMENT, BRANCH_MANAGER |
| `B2B_ORDER_ACCEPT` | Accept B2B order (seller side) | WHOLESALER_ADMIN |
| `USER_MANAGE` | Create/modify users | BRANCH_MANAGER (own branch), ORG_OWNER (all) |
| `REPORT_ALL` | Access all reports | ACCOUNTANT, ORG_OWNER |
| `SETTINGS_MODIFY` | Modify system settings | BRANCH_MANAGER, ORG_OWNER |

### 2.4 Contextual Permission Policies

Permissions may carry **conditions** evaluated at runtime:

```
DISCOUNT_APPLY_ITEM:
  CASHIER       → condition: discount_pct <= 5
  BRANCH_MANAGER→ condition: discount_pct <= 20
  ORG_OWNER     → condition: none (unlimited)

SALE_RETURN:
  amount > ₹5,000 → requires BRANCH_MANAGER approval workflow regardless of actor's role
```

---

## 3. Platform & First-Launch Requirements

### 3.1 Installation & Setup

**REQ-SETUP-001** 🔴 P1  
The application must install via a **signed MSIX package** on Windows 10 (21H2+) and Windows 11 without requiring administrator privileges for the end user (admin only for installation).

**REQ-SETUP-002** 🔴 P1  
The installer must be **offline-capable** — it should not require an internet connection to complete installation. All required runtimes (.NET 10, Windows App SDK, Visual C++ Redistributable) must be bundled or pre-checked.

**REQ-SETUP-003** 🔴 P1  
On first launch, a **7-step Setup Wizard** must run:
1. **Welcome + Language** — Language selection (English default; future: Hindi, Marathi, Tamil, Telugu, Kannada)
2. **Theme Selection** — Live preview of Light vs Dark theme; selection applies immediately
3. **Accent Color** — 8 preset accents shown as coloured circles; Pharmacy Green is default
4. **Display Density** — Compact / Normal / Comfortable preview with sample row heights
5. **Font Size** — Small / Medium / Large with live text preview
6. **Organisation Setup** — Company name, GSTIN, address, phone, branch name
7. **Done** — Navigate to POS

**AC:** Each wizard step has a Back button and a Skip button. Selections persist immediately to `AppLocalSettings`. Completing the wizard does not require internet. All 7 steps must be completable in under 3 minutes.

**REQ-SETUP-004** 🟡 P2  
The wizard must detect whether a **Local Server** is configured on the network. If found, the workstation connects to it. If not found, the workstation operates in standalone-SQLite mode with a warning that multi-user sync and LAN features are unavailable.

**REQ-SETUP-005** 🟢 P3  
During wizard step 6 (Organisation Setup), if the user enters a valid GSTIN, the system shall attempt to **auto-populate** company name and state from the GST portal (async, non-blocking; gracefully skips if offline).

### 3.2 App Shell & Navigation

**REQ-SHELL-001** 🔴 P1  
The application shell must render with a collapsible **left sidebar** (240px expanded, 56px icon-only when collapsed), a **top toolbar**, and a main **content area**. Minimum supported window size: 1280×720px.

**REQ-SHELL-002** 🔴 P1  
A **Command Palette** (Ctrl+K) must be available from every screen. It must support searching: screen names, actions, invoice numbers, product names, customer names, and batch codes. Results must appear within 100ms of keystroke.

**REQ-SHELL-003** 🔴 P1  
A **sync status indicator** must be permanently visible in the top toolbar showing: Synced ✅ / Pending (N events) 🟡 / Sync Error 🔴. Clicking it opens the sync log.

**REQ-SHELL-004** 🟡 P2  
Navigation between modules must complete in ≤150ms (cold: first visit) and ≤80ms (warm: revisit with cached view) from click to first paint.

**REQ-SHELL-005** 🟡 P2  
The application title bar must be **custom-rendered** (Mica effect allowed here only), showing: app icon, "Medistock", active branch name, and standard window controls (min/max/close).

---

## 4. Organisation & Branch Management

### 4.1 Organisation

**REQ-ORG-001** 🔴 P1  
An **Organisation** is the top-level entity (tenant). All data is scoped to `org_id`. One Medistock licence = one Organisation. Multi-branch Organisations share the same `org_id` with different `branch_id`s.

**REQ-ORG-002** 🔴 P1  
Organisation record must store: legal name, trade name, GSTIN, drug licence number (DL), address, state code, phone, email, logo (for invoices), financial year start month.

**REQ-ORG-003** 🔴 P1  
The system must support **multiple Financial Years**. Each financial year has: start date, end date, `is_active` flag. Closing a year locks all transactions in that period.

**AC:** Posted transactions in a closed financial year cannot be deleted or modified. Corrections require a new-year correction journal.

### 4.2 Branches

**REQ-BRANCH-001** 🔴 P1  
An Organisation may have **one or more Branches**. Each Branch has: name, address, GSTIN (can differ from org if branch is separately registered), DL number, contact, state code.

**REQ-BRANCH-002** 🔴 P1  
Each Branch has at least one **Warehouse**. Warehouses have Locations (rack/shelf/bin). Stock is always tracked at `(batch_id, warehouse_id, location_id)` granularity — never just product.

**REQ-BRANCH-003** 🔴 P1  
Each Branch has one or more **Counters** (POS terminals). A Counter has a name (e.g. "Counter 1", "Pharmacist Counter") and an assigned workstation device.

**REQ-BRANCH-004** 🟡 P2  
**Document Sequences** are defined per branch per document type per financial year. Format: `PREFIX-YYYY-NNNNNN` (e.g. `INV-2026-000182`). Sequence numbers are issued atomically — no two documents in the same scope can share a number.

**AC:** Simulate 10 concurrent cashiers creating sales simultaneously. All invoice numbers must be unique. Zero duplicates acceptable.

### 4.3 Devices

**REQ-DEVICE-001** 🔴 P1  
Every workstation that connects to the system must be **registered as a Device** with: device name, branch, type (CASHIER_WORKSTATION / PHARMACIST_WORKSTATION / MANAGER_WORKSTATION), last seen timestamp.

**REQ-DEVICE-002** 🔴 P1  
Every transaction must carry `device_id` — this is used for outbox idempotency and audit trails.

---

## 5. Identity, Access & Security

### 5.1 Authentication

**REQ-AUTH-001** 🔴 P1  
Users authenticate with **username + password** (hashed with bcrypt, cost factor ≥12). Passwords must meet: ≥8 characters, 1 uppercase, 1 number.

**REQ-AUTH-002** 🔴 P1  
A **PIN login mode** (4–6 digits) must be supported for quick cashier switching at the POS counter. PIN is in addition to full password login, not a replacement.

**AC:** PIN login must complete (including UI feedback) in ≤500ms from press of last digit.

**REQ-AUTH-003** 🟡 P2  
**MFA** (TOTP — Google Authenticator compatible) must be enforced for: ORG_OWNER, BRANCH_MANAGER, ACCOUNTANT roles. Optional for others.

**REQ-AUTH-004** 🔴 P1  
Authentication tokens: Access token TTL = 15 minutes. Refresh token TTL = 7 days (rotating). Revoked on logout and password change.

**REQ-AUTH-005** 🔴 P1  
After **5 consecutive failed login attempts**, the account must be temporarily locked for 15 minutes. Alert sent to ORG_OWNER.

**REQ-AUTH-006** 🟡 P2  
**Auto-lock**: Screen locks to PIN prompt after configurable idle time (default 10 minutes). In-progress POS bills are preserved (held automatically).

### 5.2 Authorisation

**REQ-AUTHZ-001** 🔴 P1  
The system must implement **Role-Based Access Control (RBAC)**: each user has one or more roles; each role has a set of permissions; permissions grant access to features.

**REQ-AUTHZ-002** 🔴 P1  
The system must implement **Attribute-Based Access Control (ABAC)** for contextual policies: discount limits, return value thresholds, report access by date range.

**REQ-AUTHZ-003** 🔴 P1  
`org_id` and `branch_id` for all server-side operations must be derived **exclusively from the authenticated JWT claims** — never from any client-submitted request body or URL parameter.

**REQ-AUTHZ-004** 🟡 P2  
A **permission denied** action must display a clear, user-friendly message explaining what permission is required and suggesting who to contact.

**REQ-AUTHZ-005** 🟡 P2  
Permission-gated UI elements (buttons, menu items) must be **visually suppressed** (hidden or greyed-out with tooltip) rather than simply throwing an error when clicked.

### 5.3 Audit

**REQ-AUDIT-001** 🔴 P1  
Every data modification (CREATE, UPDATE, state change, DELETE) must write an immutable **Audit Log** record with: user_id, device_id, action, entity_type, entity_id, old_value (JSON), new_value (JSON), timestamp, IP address.

**REQ-AUDIT-002** 🔴 P1  
Audit logs are **never deletable** — not by any user role including ORG_OWNER. Retention: minimum 6 years (GST document retention requirement).

**REQ-AUDIT-003** 🟡 P2  
Audit log is viewable (read-only) by ACCOUNTANT and ORG_OWNER from the Settings → Audit Log screen. Filterable by: user, action type, entity type, date range.

---

## 6. Product Master

### 6.1 Product Identity

**REQ-PROD-001** 🔴 P1  
A **Product** record must store:
- Internal ID (ULID), human-readable SKU
- Brand name (trade name)
- Generic name / composition
- Manufacturer (linked entity)
- Salt / active ingredient(s) (linked, multiple per product)
- Strength (e.g. "500mg", "10mg/5ml")
- Dosage form (Tablet, Capsule, Syrup, Injection, Cream, Gel, Drops, Inhaler, Patch, Suppository, etc.)
- Pack size (e.g. strip of 10, bottle of 100ml)
- Unit (TAB, CAP, ML, GM, etc.)
- Strip/unit conversion ratio (e.g. 1 strip = 10 tablets — sold at strip level, stocked at tablet level)
- HSN code
- GST rate (linked to `gst_rates` table)
- Drug schedule: OTC / Schedule-H / Schedule-H1 / Schedule-X (Narcotic)
- `is_prescription_required` (boolean)
- `is_cold_chain` (boolean — requires 2–8°C storage)
- `is_narcotic` (boolean)
- `is_active` (boolean)
- Global product ID (for central master linkage)

**AC:** Product creation form validates: HSN code format, GSTIN format if entered, pack size > 0, at least one barcode.

**REQ-PROD-002** 🔴 P1  
Each product may have **multiple barcodes** (primary barcode, secondary, company barcode, carton barcode). Any barcode must resolve to the product in ≤100ms on POS.

**REQ-PROD-003** 🔴 P1  
A **FTS5 full-text search index** on product name, generic name, brand name, salt names, and manufacturer name must be maintained as a SQLite shadow table. Updated automatically via triggers on product INSERT/UPDATE. Never rebuilt at query time.

**AC:** FTS5 search on 50,000 products returns top 20 results in ≤100ms on baseline hardware.

**REQ-PROD-004** 🔴 P1  
**Product Pricing** per branch: MRP (Maximum Retail Price), Purchase Rate (last), Sale Rate, Wholesale Rate. Rates are effective-date versioned — old rates are preserved.

**REQ-PROD-005** 🟡 P2  
**Reorder Rules** per product per warehouse: minimum stock, maximum stock, reorder point, reorder quantity, lead days. Used by the smart reorder recommendation engine.

**REQ-PROD-006** 🟡 P2  
**Alternative products** linkage: a product may be marked as a substitute for another. The POS can suggest alternatives when a product is out of stock.

**REQ-PROD-007** 🟢 P3  
A **Global Product Master** — pre-seeded database of common Indian pharmaceutical products (by brand, generic, HSN, manufacturer) — is provided as an importable starting point. Users can modify or extend it.

**REQ-PROD-008** 🟢 P3  
**Product images**: one image per product, max 2MB, compressed to 400×400px on upload. Displayed in product detail only, never in POS search results.

### 6.2 Manufacturers & Salts

**REQ-PROD-009** 🔴 P1  
**Manufacturer** entity: name, GSTIN, DL number, address, is_global (for pre-seeded global masters).

**REQ-PROD-010** 🔴 P1  
**Salt / Active Ingredient** entity: name, description. One product may have multiple salts with individual strengths (e.g. Amoxicillin 500mg + Clavulanic Acid 125mg).

### 6.3 Categories & Units

**REQ-PROD-011** 🔴 P1  
**Product Categories**: hierarchical (parent → child), e.g. Medicines → Antibiotics → Penicillins. Each category carries a default HSN code suggestion.

**REQ-PROD-012** 🔴 P1  
**Units** master: standardised unit definitions (TAB, CAP, BOT, STR, INJ, GM, ML, etc.) with abbreviations. Units are shared across all organisations (global master).

---

## 7. Inventory Management

### 7.1 Batch & Stock Model

**REQ-INV-001** 🔴 P1  
Stock is **never tracked as product → quantity alone**. Every stock record is: `product_id + batch_id + warehouse_id + location_id → {quantity, reserved_quantity, available_quantity}`.

**REQ-INV-002** 🔴 P1  
A **Batch** record stores: batch number, expiry date, manufacturing date, MRP at time of purchase, purchase rate, sale rate. One product may have many active batches simultaneously.

**REQ-INV-003** 🔴 P1  
**FEFO (First Expiry First Out)** is the default batch selection strategy for POS and B2B fulfilment. The system always suggests the batch closest to expiry (while still valid).

**REQ-INV-004** 🔴 P1  
**Expired batches** are hard-blocked from sale. The POS must not allow an expired batch to be added to the cart — this is a safety requirement, not optional.

**AC:** Attempt to add an expired batch to POS cart → system displays "Batch expired (EXP: MM/YYYY)" and refuses to add. The block cannot be overridden by any user role.

**REQ-INV-005** 🔴 P1  
**Stock deduction** for a sale must be **atomic**:
```sql
UPDATE stock_balances
SET quantity = quantity - @qty
WHERE batch_id = @batchId AND warehouse_id = @warehouseId AND quantity >= @qty
```
If affected rows = 0 (insufficient stock), the sale item must be rejected. Never allow negative stock.

**AC:** Simulate 5 cashiers selling the last unit of the same batch concurrently. Exactly 1 must succeed; the rest must receive an "insufficient stock" error.

### 7.2 Stock Movements (Ledger)

**REQ-INV-006** 🔴 P1  
Every change to stock quantity must write a **Stock Movement** record. Movement types:

| Type | Trigger |
|---|---|
| PURCHASE | GRN posted |
| SALE | Sale posted |
| SALE_RETURN | Sale return posted |
| PURCHASE_RETURN | Purchase return posted |
| STOCK_TRANSFER_OUT | Branch transfer dispatched |
| STOCK_TRANSFER_IN | Branch transfer received |
| ADJUSTMENT | Manual stock adjustment |
| DAMAGE | Damage write-off |
| EXPIRY_WRITEOFF | Expired stock written off |
| OPENING | Opening stock entry |
| CORRECTION | Supervisor correction |

**AC:** For any product batch, summing all stock movements must equal current stock balance. This is verifiable at any time.

### 7.3 Stock Reservations

**REQ-INV-007** 🟡 P2  
When a B2B order is confirmed, the committed quantity must be **reserved** in `stock_reservations`. `available_quantity = quantity - reserved_quantity`. POS only draws from `available_quantity`.

**REQ-INV-008** 🟡 P2  
Reservations that are not fulfilled within a configurable timeout (default 24 hours) are automatically released and the relevant B2B order status updated.

### 7.4 Expiry Management

**REQ-INV-009** 🔴 P1  
The **Expiry Dashboard** must categorise all batches into bands:
- 🔴 Expired (expiry date < today)
- 🔴 Critical: ≤30 days to expiry
- 🟡 Warning: 31–90 days to expiry
- 🟢 OK: >90 days to expiry

Each band shows: count of batches, total quantity at risk, total ₹ value at risk.

**AC:** Dashboard loads with accurate data in ≤2 seconds for 10,000+ batch records.

**REQ-INV-010** 🟡 P2  
**Automated daily alerts** (morning 9AM) notify BRANCH_MANAGER and STOREKEEPER of: new expired batches, batches entering critical band, and batches entering warning band. Delivered via in-app notification and optionally SMS/email.

### 7.5 Stock Adjustments

**REQ-INV-011** 🔴 P1  
**Stock Adjustments** require: product, batch, adjustment type (ADD / REDUCE), quantity, reason (mandatory text), authorised by (BRANCH_MANAGER or above). Each adjustment writes a stock movement and an audit log.

**AC:** A CASHIER role attempting a stock adjustment receives "Permission denied: STOCK_ADJUST required."

### 7.6 Inter-Branch Stock Transfers

**REQ-INV-012** 🟡 P2  
**Stock Transfers** between branches follow a state machine:
```
DRAFT → SUBMITTED → APPROVED → PICKING → DISPATCHED → RECEIVED → COMPLETED
Exception: REJECTED / CANCELLED
```

**REQ-INV-013** 🟡 P2  
On DISPATCHED: stock is deducted from source branch (movement: STOCK_TRANSFER_OUT). On RECEIVED: stock is added to destination branch (movement: STOCK_TRANSFER_IN). Discrepancy between dispatched and received quantities must be flagged for BRANCH_MANAGER resolution.

### 7.7 Stock Reports

**REQ-INV-014** 🔴 P1  
**Stock Summary** report: product, batch, warehouse, location, qty, reserved qty, available qty, MRP value, purchase value. Filterable by category, manufacturer, expiry band, stock status, warehouse.

**REQ-INV-015** 🟡 P2  
**Stock Movement History** per product/batch: full ledger showing all movements with date, type, reference, quantity change, running balance.

**REQ-INV-016** 🟡 P2  
**Reorder Report**: list of products where available_quantity ≤ reorder_point. Shows: product, current stock, reorder point, reorder qty, last purchase rate, primary supplier.

---

## 8. Point of Sale (POS)

> The POS is the most performance-critical screen in the entire application. All performance requirements from `trd.md` apply with maximum force here.

### 8.1 POS Layout & Access

**REQ-POS-001** 🔴 P1  
The POS screen must be a **dedicated full-screen workspace** — not a generic CRUD screen. Layout: two-panel split (search/results left, running bill right). This layout cannot be accidentally navigated away from during an active sale.

**REQ-POS-002** 🔴 P1  
POS must be accessible with a single keyboard shortcut from any screen: **F2** (Medistock Standard profile) or **Alt+A** (MARG-Compatible profile).

### 8.2 Product Search & Add to Cart

**REQ-POS-003** 🔴 P1  
The product search field must be focused and ready to accept input **immediately** when POS screen loads. Focus must return to search field automatically after every cart action.

**REQ-POS-004** 🔴 P1  
Product search must query the **local SQLite FTS5 index** only — never a network call. Results must appear within **100ms** of each keystroke (150ms hard ceiling).

**REQ-POS-005** 🔴 P1  
Search must match against: product name (brand), generic name, salt name, manufacturer, barcode, SKU, internal product code.

**REQ-POS-006** 🔴 P1  
**Barcode scanning**: the POS must detect a barcode scan (complete string terminated by `Enter` within 100ms) as distinct from keyboard typing, and immediately resolve it to a product+batch and add to cart without requiring user selection from a list.

**AC:** Scan a valid barcode → product with its FEFO batch is added to cart within 200ms. No intermediate screen.

**REQ-POS-007** 🔴 P1  
Search results list must display: product name, generic name, current available stock (colour-coded: green=OK, amber=low, red=out), MRP, batch expiry (closest). Maximum 20 results shown.

**REQ-POS-008** 🔴 P1  
**Safety gates in search results**:
- Expired batches: shown with strikethrough, cannot be selected
- Zero stock: shown in red, adding triggers "Out of stock" warning (soft block — can override for orders)
- Schedule-H: marked with purple `[Sch-H]` badge — requires prescription reference before adding
- Narcotic/Schedule-X: shows hard warning; requires doctor registration on file; logged mandatorily

**REQ-POS-009** 🟡 P2  
When a product is out of stock, the system must suggest **alternative products** (same salt, similar dosage form) from within the same category, sorted by availability.

### 8.3 Cart Management

**REQ-POS-010** 🔴 P1  
The cart (running bill) panel must show in real-time: each line item (product, batch, qty, MRP, discount, GST, net amount), subtotal, total GST, total discount, round-off, and **grand total** — all updating instantly on every change.

**REQ-POS-011** 🔴 P1  
**Quantity editing**: clicking/selecting a cart item allows inline qty edit. Tab moves between editable fields. Quantity must accept: whole numbers and half-units (0.5) for applicable products.

**REQ-POS-012** 🔴 P1  
Cart items must support: edit quantity, change batch (if multiple batches available), apply item-level discount (permission-gated), remove item.

**REQ-POS-013** 🔴 P1  
**Strip/Tablet conversion**: if a product has strip size defined (e.g. 1 strip = 10 tablets), the user can enter quantity in strips or tablets — system converts and stores at base unit level.

**REQ-POS-014** 🟡 P2  
**Hold Bill / Resume Bill**: F5 holds the current bill (saved as a draft with a hold reference). Up to 5 bills can be held simultaneously per counter. Ctrl+H lists held bills; selecting one resumes it. The search field clears for a new bill.

**AC:** Hold a bill mid-transaction. Log out. Log in as same user. Resume bill. All items, quantities, discounts, and customer linkage must be exactly preserved.

**REQ-POS-015** 🟡 P2  
**Customer linkage**: F8 opens customer search. Linking a customer to the bill enables credit sales, loyalty points, and targeted schemes.

### 8.4 Discount & Scheme Engine

**REQ-POS-016** 🔴 P1  
**Item-level discount**: applies to a single cart item. Must be permission-gated by role (CASHIER ≤5%, BRANCH_MANAGER ≤20%, ORG_OWNER unlimited). Discounts exceeding the actor's limit trigger an **approval workflow** — a manager is prompted to enter their PIN to approve.

**REQ-POS-017** 🔴 P1  
**Bill-level discount**: applies uniformly across all cart items after individual discounts. Same permission gates as item-level.

**REQ-POS-018** 🟡 P2  
**Scheme Engine**: automatic application of configured promotion rules:
- **Percentage discount**: 10% off all Paracetamol tablets
- **Buy X get Y**: Buy 3 strips, get 1 free
- **Free quantity**: Buy any antibiotic, get hand sanitiser free
- **Category scheme**: 5% off all OTC products above ₹500 bill value
- **Manufacturer scheme**: 2% extra discount on ABC Pharma products this month

Schemes must have effective date ranges and priority ordering (highest priority scheme wins if multiple apply).

**AC:** Configure a "Buy 2 Crocin strips get 1 free" scheme. Add 2 strips to cart. Third strip must appear as a free line item (₹0) automatically.

**REQ-POS-019** 🟡 P2  
**Manual scheme load**: allow user to manually apply a pre-configured scheme bundle (saved item sets) via Alt+F12.

### 8.5 Payment

**REQ-POS-020** 🔴 P1  
The payment screen (F6) must support payment modes:
- **Cash** — shows change amount (₹ received − ₹ total), updated live as user types
- **Card** — enter card last 4 digits and approval code (optional)
- **UPI** — enter UPI reference/transaction ID
- **Credit (Ledger)** — post against customer's running account balance
- **Split** — any combination of the above modes

**REQ-POS-021** 🔴 P1  
The **Change Calculator** for cash payments: user types amount received; change is shown instantly in large font (FontSize28). Must be accurate to the paise.

**REQ-POS-022** 🔴 P1  
After confirming payment (F6 / Enter), the sale must be:
1. Committed atomically to local database (sale + items + payment + stock deduction + journal + audit + outbox event — all in one transaction)
2. Invoice printed (if printer connected)
3. Cart cleared
4. Search field focused
5. All within **≤500ms** from payment confirmation

**AC:** Disconnect internet and local server. Complete a sale. Verify: sale saved in SQLite, stock balance decremented, outbox event in PENDING status. Reconnect. Verify: outbox event syncs successfully.

**REQ-POS-023** 🟢 P3  
**Partial payment / advance**: accept partial amount; post remaining as credit against customer ledger.

### 8.6 Invoice Generation & Print

**REQ-POS-024** 🔴 P1  
**Invoice format** (both printed and PDF) must include:
- Organisation name, GSTIN, DL number, address
- Invoice number, date, time
- Counter / cashier name
- Customer name, GSTIN (if provided)
- Line items: product name, batch no, expiry, qty, MRP, discount, taxable amount, GST rate, GST amount, net amount
- Tax summary: CGST total, SGST total, IGST total (or only IGST for interstate)
- Payment mode breakdown
- Grand total in digits and words
- QR code (UPI payment / IRN after e-Invoice)
- Terms: "Goods once sold will not be taken back" (if configured)
- Pharmacist name and registration number (for prescription items)

**REQ-POS-025** 🔴 P1  
Print trigger to printed receipt must complete in ≤300ms (hard ceiling 500ms) from print command.

**REQ-POS-026** 🟡 P2  
**PDF invoice** generation — saved to local storage and optionally emailed/WhatsApp'd to the customer.

**REQ-POS-027** 🟡 P2  
Invoice must be re-printable from Sales History at any time. Re-printing is logged in audit.

### 8.7 Prescription Management at POS

**REQ-POS-028** 🔴 P1  
When a Schedule-H or Schedule-H1 product is added to the cart, the system must **require a prescription reference** before the sale can be completed. The reference may be:
- Prescription number (typed)
- Patient name + doctor name
- Link to an existing Prescription record in the system

**REQ-POS-029** 🔴 P1  
**Narcotic / Schedule-X drugs** require: doctor name, doctor registration number, patient name, prescription reference. The sale is **mandatorily logged** in a Narcotic Sale Register regardless of other settings.

**REQ-POS-030** 🟡 P2  
A **prescription can be scanned** (QR or barcode from the physical prescription paper). The system links the prescription record to the sale.

---

## 9. Sales History & Returns

### 9.1 Sales History

**REQ-SALE-001** 🔴 P1  
**Sales History** screen lists all posted invoices for the active branch, filterable by: date range, invoice number, customer name, product, cashier, payment mode, status.

**REQ-SALE-002** 🔴 P1  
Clicking an invoice opens the **Invoice Detail View** showing: all line items, payment breakdown, linked patient/doctor if applicable, GST summary, audit trail for this invoice.

**REQ-SALE-003** 🔴 P1  
A posted invoice **cannot be deleted or modified**. The only correction paths are: Sale Return (full or partial) or credit note.

### 9.2 Sale Returns

**REQ-SALE-004** 🔴 P1  
**Sale Return** allows returning any subset of items from a posted invoice. The return must:
1. Reference the original invoice
2. Record return reason (mandatory)
3. Record restocking decision for each item (Restock → back to inventory / Damage → write-off / Quarantine → pending decision)
4. Generate a **Credit Note** with a unique CN number
5. Reverse the accounting journal entries
6. Reverse GST entries

**REQ-SALE-005** 🔴 P1  
Returns require the `SALE_RETURN` permission. Returns above ₹5,000 require `BRANCH_MANAGER` approval regardless of the initiating user's role.

**REQ-SALE-006** 🟡 P2  
Sale return against cash payment results in a **cash refund**. Sale return against credit payment results in a credit adjustment in the customer's ledger. Both paths must be handled.

**REQ-SALE-007** 🟡 P2  
**Narcotic sale returns** are logged in the Narcotic Sale Register with reason.

---

## 10. Purchase & Procurement

### 10.1 Purchase Requisition

**REQ-PUR-001** 🟡 P2  
A **Purchase Requisition** can be created by STOREKEEPER or PROCUREMENT staff to request stock. It contains: product, requested quantity, reason, target supplier (optional).

**REQ-PUR-002** 🟡 P2  
Requisitions are approved by BRANCH_MANAGER. Approved requisitions can be converted to Purchase Orders.

### 10.2 Purchase Order

**REQ-PUR-003** 🔴 P1  
A **Purchase Order** is sent to a supplier. It contains: PO number, PO date, supplier, expected delivery date, line items (product, quantity, expected rate). PO status: DRAFT → SENT → PARTIALLY_RECEIVED → RECEIVED → CANCELLED.

**REQ-PUR-004** 🟡 P2  
PO can be **printed** and emailed to supplier. PDF format with organisation header, supplier address, PO number, itemised list.

### 10.3 Goods Receipt Note (GRN)

**REQ-PUR-005** 🔴 P1  
When goods arrive, a **Goods Receipt Note (GRN)** is created against the PO. GRN contains: received date, line items with batch number, expiry date, quantity received, condition notes.

**REQ-PUR-006** 🔴 P1  
**Batch data entry at GRN**:
- Product (auto-filled from PO)
- Batch number (mandatory)
- Manufacturing date
- Expiry date (mandatory — validated: must be in future, must be ≥ minimum shelf-life for category if configured)
- Quantity received
- MRP printed on pack
- Purchase rate per unit
- Free goods quantity (if any)

**AC:** Barcode scan at GRN fills in the product field. Tab moves cursor to batch number field for efficient data entry.

**REQ-PUR-007** 🔴 P1  
Posting the GRN must:
1. Add stock to `stock_balances` for each batch/warehouse
2. Create `stock_movements` records (type: PURCHASE)
3. Write outbox event for cloud sync

### 10.4 Purchase Invoice

**REQ-PUR-008** 🔴 P1  
A **Purchase Invoice** is created from the GRN. It records: supplier invoice number, supplier invoice date, line items with GST breakdown (taxable value, CGST/SGST/IGST), total, payment terms.

**REQ-PUR-009** 🔴 P1  
Posting the Purchase Invoice must:
1. Create accounting journal entries: Dr Inventory + Dr Input GST Cr Supplier Payable
2. Create `gst_transactions` record
3. Write outbox event

**REQ-PUR-010** 🔴 P1  
**Posted purchase invoices are immutable.** Corrections require a Purchase Return (for returned goods) or Debit Note (for price corrections).

### 10.5 Purchase Returns & Debit Notes

**REQ-PUR-011** 🟡 P2  
**Purchase Return**: when goods are returned to supplier. Must: deduct stock, generate Debit Note, reverse accounting journal entries, create GST transaction (negative).

**REQ-PUR-012** 🟡 P2  
**Debit Note** (without goods return): for price corrections or shortages. Reduces supplier payable. Does not affect stock.

---

## 11. Accounting Engine

### 11.1 Chart of Accounts

**REQ-ACC-001** 🔴 P1  
The system must maintain a **Chart of Accounts** (CoA) per organisation with accounts of type: ASSET / LIABILITY / EQUITY / REVENUE / EXPENSE. Accounts are hierarchical (parent → child).

**REQ-ACC-002** 🔴 P1  
A **default CoA** must be pre-seeded on organisation creation, including:
- Assets: Cash, Bank, Accounts Receivable, Inventory, Fixed Assets
- Liabilities: Accounts Payable, GST Payable (CGST, SGST, IGST), TDS Payable, Other Liabilities
- Equity: Owner's Capital, Retained Earnings
- Revenue: Sales, Sales Returns, Other Income
- Expenses: Purchases, Purchase Returns, Salaries, Rent, Utilities, Depreciation, Other Expenses

**REQ-ACC-003** 🟡 P2  
Accounts marked `is_system = true` (e.g. Sales, Inventory, GST Payable) cannot be deleted. Their names and types cannot be changed.

### 11.2 Journal & Double-Entry

**REQ-ACC-004** 🔴 P1  
**Every financial event** must produce a balanced Journal Entry (Debit total = Credit total). The system auto-generates journals for:

| Event | Debit | Credit |
|---|---|---|
| Cash Sale | Cash | Sales + GST Payable |
| Credit Sale | Accounts Receivable | Sales + GST Payable |
| Cash Purchase | Inventory + Input GST | Cash |
| Credit Purchase | Inventory + Input GST | Accounts Payable |
| Cash payment received | Cash | Accounts Receivable |
| Payment to supplier | Accounts Payable | Cash/Bank |
| Sale Return (cash) | Sales + GST Payable | Cash |
| Purchase Return | Accounts Payable | Inventory + Input GST |
| Damage write-off | Loss on Damage | Inventory |
| Expiry write-off | Loss on Expiry | Inventory |

**AC:** For any 30-day period, Trial Balance must balance (Total Debits = Total Credits) to the paise.

**REQ-ACC-005** 🔴 P1  
**Financial Immutability**: Once a journal is in `POSTED` status, no line can be edited or deleted. Corrections require a **Reversal Journal** (mirror entry with opposite signs) followed by a corrected journal.

**REQ-ACC-006** 🟡 P2  
**Manual Journal Entry**: ACCOUNTANT may create manual journals for: adjustments, depreciation, opening entries, provisions. Manual journals require a narration and an approver for amounts above a configurable threshold.

### 11.3 Ledger

**REQ-ACC-007** 🔴 P1  
A **Ledger** is maintained per account, showing chronological journal postings with running balance. Ledger must be queryable by: account, date range, financial year.

**REQ-ACC-008** 🟡 P2  
**Customer Ledger**: Shows all sales, returns, credit notes, payments, and running outstanding balance for a customer.

**REQ-ACC-009** 🟡 P2  
**Supplier Ledger**: Shows all purchase invoices, debit notes, payments, and running outstanding balance for a supplier.

### 11.4 Payments & Allocation

**REQ-ACC-010** 🔴 P1  
**Payment records** are independent of invoices. A payment may settle one or many invoices. **Payment allocation** maps payment → invoice(s) with amounts.

**REQ-ACC-011** 🔴 P1  
**Accounts Receivable ageing**: list of customers with outstanding amounts grouped by: current, 1–30 days overdue, 31–60 days, 61–90 days, 90+ days.

**REQ-ACC-012** 🔴 P1  
**Accounts Payable ageing**: same structure for suppliers.

### 11.5 Financial Reports

**REQ-ACC-013** 🟡 P2  
**Trial Balance**: all accounts with debit/credit totals, balanced. Filterable by date range and financial year.

**REQ-ACC-014** 🟡 P2  
**Profit & Loss Statement**: Revenue − Cost of Goods − Expenses = Net Profit. Monthly/quarterly/annual views.

**REQ-ACC-015** 🟡 P2  
**Balance Sheet**: Assets = Liabilities + Equity. Snapshot as of any date.

**REQ-ACC-016** 🟢 P3  
**Cash Flow Statement**: Operating / Investing / Financing activities.

**REQ-ACC-017** 🟢 P3  
**Bank Reconciliation**: compare bank statement entries against Cash/Bank account transactions. Mark reconciled, highlight unmatched items.

---

## 12. GST & Compliance

### 12.1 GST Calculation

**REQ-GST-001** 🔴 P1  
GST rates must be stored in a **versioned, configurable table** (`gst_rates`) — never hardcoded. Each rate has: rate_pct, cgst_pct, sgst_pct, igst_pct, hsn_range, effective_from, effective_to.

**REQ-GST-002** 🔴 P1  
GST type determination per transaction:
- Buyer GSTIN state = Seller GSTIN state → **CGST + SGST** (intra-state)
- Buyer GSTIN state ≠ Seller GSTIN state → **IGST** (inter-state)
- Retail sale (no buyer GSTIN) → **CGST + SGST** (same state assumed)

**REQ-GST-003** 🔴 P1  
GST is calculated **at line-item level** (not bill level), using the HSN rate for each product. The system computes: taxable_value, cgst_amount, sgst_amount, igst_amount, total_gst per item, and sums them for the bill.

**REQ-GST-004** 🔴 P1  
All GST calculations must be accurate to **two decimal places** per line item, with proper rounding at bill total level.

### 12.2 E-Invoice

**REQ-GST-005** 🟡 P2  
For organisations with annual turnover ≥ ₹5 crore (configurable threshold), **e-Invoice generation** is mandatory. After each sale, the system must:
1. Complete the local sale (synchronous, ≤50ms)
2. Queue an `E_INVOICE_REQUIRED` outbox event
3. Background worker posts to GST IRP/NIC API
4. On success: store IRN + QR code, update invoice record
5. On failure: retry with exponential backoff; alert ACCOUNTANT if persistently failing

**AC:** GST IRP API is down. Sale must still complete locally in ≤50ms. IRN is backfilled when API recovers.

**REQ-GST-006** 🟢 P3  
**E-Invoice cancellation**: if an invoice is cancelled within 24 hours, the system must cancel the IRN via the API.

### 12.3 E-Way Bill

**REQ-GST-007** 🟡 P2  
**E-Way Bill** is auto-triggered for consignments where: value ≥ ₹50,000 and goods are being transported. Generated via GST portal API, same async outbox pattern as e-Invoice.

**REQ-GST-008** 🟢 P3  
E-Way Bill details (EWB number, expiry, vehicle, transporter) are stored and printable with the consignment.

### 12.4 GSTR Returns

**REQ-GST-009** 🟡 P2  
**GSTR-1** (outward supplies): monthly/quarterly summary of all sales with GST. System generates the complete GSTR-1 data in JSON format (IFF/GSTR-1) per GST portal specifications.

**REQ-GST-010** 🟡 P2  
**GSTR-3B** (summary return): system generates the GSTR-3B worksheet with: outward supplies, input tax credit (ITC), net tax payable.

**REQ-GST-011** 🟢 P3  
**GSTR-2B reconciliation**: import supplier's GSTR-2A/2B data and reconcile against local purchase records. Flag mismatches for ACCOUNTANT review.

### 12.5 Input Tax Credit (ITC)

**REQ-GST-012** 🟡 P2  
**ITC tracking**: for each purchase invoice, input GST (CGST + SGST or IGST) is tracked separately as `Input Tax Credit`. ITC eligible / ITC ineligible split (ineligible for certain categories like personal use).

**REQ-GST-013** 🟡 P2  
**ITC set-off**: at GST filing time, show: Output GST payable − ITC Available = Net GST payable.

### 12.6 TDS

**REQ-GST-014** 🟢 P3  
**TDS on purchases** (if pharmacy is a TDS deductor): auto-calculate TDS on applicable purchase invoices, track TDS payable, generate TDS certificates.

---

## 13. Suppliers & Accounts Payable

**REQ-SUP-001** 🔴 P1  
**Supplier** record: name, GSTIN, Drug Licence (DL) number(s), address, contact person, phone, email, credit limit, credit days, payment terms, bank account details.

**REQ-SUP-002** 🔴 P1  
**Outstanding balance** per supplier is always current (computed from posted invoices − payments). Never stale.

**REQ-SUP-003** 🔴 P1  
**Supplier Payment**: cash or bank transfer. Linked to one or more invoices. Generates journal: Dr Accounts Payable Cr Cash/Bank. Updates outstanding balance immediately.

**REQ-SUP-004** 🟡 P2  
**Credit limit enforcement**: when creating a purchase order, if supplier outstanding + PO value > credit limit, display a prominent warning. Blocking vs non-blocking is configurable.

**REQ-SUP-005** 🟡 P2  
**Supplier statement**: printable/emailable statement showing: invoices, debit notes, payments, running balance — for any date range.

**REQ-SUP-006** 🟢 P3  
**Supplier performance** metrics: on-time delivery rate, shortage rate, quality rejection rate, average lead time — derived from GRN data.

---

## 14. Customers, Patients & CRM

### 14.1 Customers

**REQ-CRM-001** 🔴 P1  
**Customer** record: name, phone (unique per org), email, GSTIN, address, loyalty points balance, credit limit, outstanding balance, is_active.

**REQ-CRM-002** 🔴 P1  
Customer search at POS (F8) must be by: name, phone number, GSTIN. Must return results in ≤100ms.

**REQ-CRM-003** 🟡 P2  
**Credit customer sales**: a customer may have a credit limit. Sales on credit are posted against their ledger. Credit block if outstanding ≥ credit limit (configurable to warn-only).

**REQ-CRM-004** 🟡 P2  
**Loyalty Points**: configurable earn rate (e.g. 1 point per ₹10 spent). Points redeemable at POS for discount (configurable redemption rate). Points never expire unless configured.

### 14.2 Patients

**REQ-CRM-005** 🟡 P2  
**Patient** record: name, date of birth, gender, phone, email, linked doctor (primary), prescription history, purchase history, chronic medication flags.

**REQ-CRM-006** 🟡 P2  
**Prescription refill reminders**: for patients with chronic medications (flagged by pharmacist), automated reminders at configurable intervals (e.g. 25 days after last purchase of a 30-day course).

### 14.3 Doctors

**REQ-CRM-007** 🟡 P2  
**Doctor** record: name, qualification, registration number (MCI/state council), specialisation, phone, address, clinic name. Required for Schedule-H sales and prescription linkage.

**REQ-CRM-008** 🟢 P3  
**Doctor analytics**: sales of products linked to prescriptions from each doctor. Useful for pharmacist relationship management.

---

## 15. B2B Commerce Network

### 15.1 Overview & Roles

The B2B network connects Pharmacies (buyers) to Wholesalers/Distributors (sellers) on the same Medistock platform. A Wholesaler's ERP is also Medistock — same product, different role configuration.

### 15.2 B2B Transaction State Machine

**REQ-B2B-001** 🔴 P1 (Phase 7)  
Every B2B transaction follows this state machine:

```
DRAFT → SUBMITTED → ACCEPTED → CONFIRMED → PICKING → PACKED
→ READY_FOR_PICKUP → HANDED_OVER → IN_TRANSIT → DELIVERED
→ RECEIVED → INVOICED → COMPLETED

Exception states:
REJECTED | CANCELLED | PARTIALLY_ACCEPTED | PARTIALLY_RECEIVED |
DISPUTED | RETURN_REQUESTED | RETURNED
```

**AC:** No transaction can skip states. State transitions must be validated server-side. Client cannot set arbitrary status values.

### 15.3 Buyer Side (Pharmacy)

**REQ-B2B-002** 🟡 P2 (Phase 7)  
**Purchase Request creation**: pharmacy creates a purchase request to a wholesaler with: wholesaler name, delivery/pickup preference, list of products with quantities and target prices.

**REQ-B2B-003** 🟡 P2 (Phase 7)  
**B2B Marketplace**: pharmacy can search registered wholesalers by product availability, price, and terms. Compare 3 wholesalers side-by-side.

**REQ-B2B-004** 🟡 P2 (Phase 7)  
**Receiving goods**: when B2B delivery arrives, pharmacy performs a **Goods Receipt** against the B2B transaction. Records actual quantities received per batch.

**REQ-B2B-005** 🟡 P2 (Phase 7)  
**Discrepancy handling**: if received quantity ≠ expected quantity, a discrepancy record is created automatically. Pharmacy can: accept with note, raise dispute, request delivery of shortfall.

### 15.4 Seller Side (Wholesaler)

**REQ-B2B-006** 🟡 P2 (Phase 7)  
**Order acceptance**: wholesaler reviews each purchase request. Can accept fully, partially (with notes on unavailable items), or reject with reason.

**REQ-B2B-007** 🟡 P2 (Phase 7)  
**Pick List generation**: on CONFIRMED status, warehouse system generates a pick list assigned to a picker. Shows: product, batch (FEFO), location, quantity.

**REQ-B2B-008** 🟡 P2 (Phase 7)  
**Packing**: picker confirms quantities picked. System generates package record with QR code. Package QR encodes: transaction ID, package ID, item manifest.

**REQ-B2B-009** 🟡 P2 (Phase 7)  
**Delivery method selection**: DELIVERY (own vehicle), PICKUP (buyer collects), COURIER (third party).

**REQ-B2B-010** 🟡 P2 (Phase 7)  
**Pickup verification**: when buyer's representative arrives to collect:
1. Scan package QR code
2. Verify: buyer's representative name + OTP (sent to buyer's registered phone) OR employee QR badge
3. Record: `otp_verified = true`, `collected_by`, `collected_at`
4. Status → HANDED_OVER
5. Sales Invoice auto-generated

**AC:** OTP verification must complete within 10 seconds. Wrong OTP → 3 attempts then lock for 5 minutes.

**REQ-B2B-011** 🟡 P2 (Phase 7)  
After goods are received and verified, seller generates **Sales Invoice** and buyer records **Purchase Invoice** — both linked by the B2B transaction ID.

### 15.5 B2B Transaction Timeline

**REQ-B2B-012** 🟡 P2 (Phase 7)  
Every B2B transaction must show a **visual timeline** of all events (with timestamps, actor, and event description) visible to both buyer and seller. Events are push-synced via outbox pattern — both parties see real-time status.

### 15.6 B2B Returns

**REQ-B2B-013** 🟢 P3 (Phase 7)  
**B2B Return flow**: pharmacy initiates return request → wholesaler accepts/rejects → goods collected/delivered back → credit note issued → inventory adjusted on both sides.

---

## 16. Multi-Branch Management

**REQ-MB-001** 🟡 P2 (Phase 6)  
An ORG_OWNER can view **consolidated reports** across all branches: total sales, total stock value, total outstanding, GST summary — all aggregated in the cloud dashboard.

**REQ-MB-002** 🟡 P2 (Phase 6)  
**Branch isolation**: a user assigned to Branch A cannot view data from Branch B unless explicitly granted cross-branch permission by ORG_OWNER.

**REQ-MB-003** 🟡 P2 (Phase 6)  
**Central Product Master**: ORG_OWNER can push product updates (pricing, GST rate changes) to all branches simultaneously. Branches receive updates via cloud sync.

**REQ-MB-004** 🟡 P2 (Phase 6)  
**Cross-branch stock view**: ORG_OWNER and BRANCH_MANAGER (with permission) can see stock levels at other branches — useful for directing customers to an alternate location.

---

## 17. Reports & Analytics

### 17.1 Sales Reports

**REQ-RPT-001** 🔴 P1  
**Daily Sales Summary**: total sales, total returns, net sales, GST collected, payment mode breakdown. Exportable to PDF/Excel.

**REQ-RPT-002** 🔴 P1  
**Sales Register**: all invoices for a date range with: invoice no, date, customer, amount, GST, payment mode. Filterable by cashier, customer, product category.

**REQ-RPT-003** 🟡 P2  
**Product-wise Sales**: top/bottom N products by quantity and value for a period.

**REQ-RPT-004** 🟡 P2  
**Doctor-wise Sales**: sales of prescription products linked to each doctor.

**REQ-RPT-005** 🟡 P2  
**Customer-wise Sales**: purchase history and outstanding balance per customer.

**REQ-RPT-006** 🟢 P3  
**Cashier Performance**: sales per cashier, average bill value, discount given, returns processed.

### 17.2 Inventory Reports

**REQ-RPT-007** 🔴 P1  
**Stock Summary**: current stock per product/batch/warehouse with value. Filterable by category, manufacturer, expiry status.

**REQ-RPT-008** 🟡 P2  
**Stock Movement Report**: all movements for a product/batch/period with running balance.

**REQ-RPT-009** 🟡 P2  
**Slow-Moving / Fast-Moving** report: products sorted by velocity (units sold per day) over a period.

**REQ-RPT-010** 🟡 P2  
**Expiry Risk Report**: batches expiring in configurable bands (30/60/90 days) with stock quantity and ₹ value at risk.

**REQ-RPT-011** 🟢 P3  
**Reorder List**: products where available stock ≤ reorder point, with suggested order quantity.

### 17.3 Purchase Reports

**REQ-RPT-012** 🔴 P1  
**Purchase Register**: all purchase invoices for a period with supplier, invoice no, amount, GST, payment status.

**REQ-RPT-013** 🟡 P2  
**Supplier-wise Purchase**: total purchases, outstanding, payment history per supplier.

### 17.4 Financial Reports

**REQ-RPT-014** 🟡 P2  
All financial reports (Trial Balance, P&L, Balance Sheet) must be generatable for any date range and downloadable as PDF or Excel.

### 17.5 GST Reports

**REQ-RPT-015** 🟡 P2  
**GST Summary**: CGST, SGST, IGST totals per period (output/input). Used for GSTR-3B filing.

**REQ-RPT-016** 🟡 P2  
**GSTR-1 Data Export**: structured JSON in GST portal format. Ready to upload.

### 17.6 Report Infrastructure

**REQ-RPT-017** 🔴 P1  
All reports must support export to: **Excel (.xlsx)**, **PDF**, **CSV**. Export must complete in ≤10 seconds for up to 100,000 records.

**REQ-RPT-018** 🟡 P2  
Reports with large datasets must show a **progress indicator** and must not freeze the UI during generation.

**REQ-RPT-019** 🟡 P2  
Reports must be **printable** directly (Ctrl+P) with proper page breaks, headers, footers (organisation name, report name, page number, print date).

---

## 18. Notifications & Alerts

**REQ-NOTIF-001** 🔴 P1  
**In-app notification centre** (bell icon with unread count): stores all notifications for the logged-in user. Notifications are role-aware (CASHIER only sees POS-relevant notifications).

**REQ-NOTIF-002** 🔴 P1  
**Toast notifications** appear for real-time events:
- Sale saved ✅
- Low stock alert (triggered when stock drops below reorder point)
- Sync failure ⚠
- Approval required (manager must approve discount/return)

**REQ-NOTIF-003** 🟡 P2  
**Configurable alert channels**: each alert type can be delivered via:
- In-app (always)
- SMS (optional, requires SMS gateway config)
- WhatsApp Business API (optional)
- Email (optional)

**REQ-NOTIF-004** 🟡 P2  
**Business Event Alerts** that must be supported:
- Stock goes below reorder point → alert STOREKEEPER
- Stock reaches zero → alert BRANCH_MANAGER + STOREKEEPER
- Batch expiry in 30 days → alert STOREKEEPER + PHARMACIST
- Outbox sync failing for > 1 hour → alert ORG_OWNER
- Login failure > 5 attempts → alert ORG_OWNER
- B2B order status change → notify both buyer and seller
- GST return due in 3 days → alert ACCOUNTANT + ORG_OWNER
- Customer outstanding overdue → alert ACCOUNTANT

**REQ-NOTIF-005** 🟢 P3  
**Patient reminders**: prescription refill reminders sent to patient's phone (SMS/WhatsApp) at configurable intervals.

---

## 19. Settings & Configuration

### 19.1 Organisation Settings

**REQ-SET-001** 🔴 P1  
**Organisation profile**: edit company name, GSTIN, DL number, address, logo, financial year start month, state code.

**REQ-SET-002** 🔴 P1  
**Branch settings**: per-branch GSTIN, DL number, contact info, warehouse configuration.

**REQ-SET-003** 🟡 P2  
**Invoice settings**: invoice header text, footer text, terms & conditions, printer default (thermal / A4 / A5), signature field, logo display.

**REQ-SET-004** 🟡 P2  
**Pricing policy**: whether MRP can be exceeded in sales (default: no). Whether discounts require approval above thresholds. Whether price overrides are logged.

### 19.2 Tax Settings

**REQ-SET-005** 🔴 P1  
**GST rate management**: add/edit/deactivate GST rates by HSN range and effective date. This is the only place tax rates are maintained — never in product records directly.

**REQ-SET-006** 🟡 P2  
**GST API credentials**: enter GST portal credentials for e-Invoice and e-Way Bill API integration. Test connection button.

### 19.3 Appearance Settings

**REQ-SET-007** 🔴 P1  
**Theme toggle**: Light / Dark. Applies instantly, no restart. Stored in `AppLocalSettings`.

**REQ-SET-008** 🔴 P1  
**Accent color**: 8 presets. Applies instantly.

**REQ-SET-009** 🔴 P1  
**Display density**: Compact / Normal / Comfortable. Applies instantly.

**REQ-SET-010** 🔴 P1  
**Font size**: Small / Medium / Large. Applies instantly.

**REQ-SET-011** 🟡 P2  
**Number format**: Indian (₹1,00,000) or Global (₹100,000). Date format: DD/MM/YYYY (default), MM/DD/YYYY, or YYYY-MM-DD.

### 19.4 Keyboard Settings

**REQ-SET-012** 🔴 P1  
**Keymap profile**: Medistock Standard (default) / MARG-Compatible. Applies instantly.

**REQ-SET-013** 🟢 P3  
**Custom keymap**: user can re-bind individual shortcuts. Changes stored in a custom profile. Option to reset to default.

### 19.5 Hardware Settings

**REQ-SET-014** 🟡 P2  
**Barcode Scanner**: test scan field to verify scanner is sending data correctly.

**REQ-SET-015** 🟡 P2  
**Receipt Printer**: select printer, paper size (80mm / 58mm / A4 / A5), test print.

**REQ-SET-016** 🟡 P2  
**Cash Drawer**: configure drawer kick port (via printer COM port or USB). Test open button.

### 19.6 Sync Settings

**REQ-SET-017** 🔴 P1  
**Local Server connection**: host, port, test connection. If connected, show: ping, current outbox queue size, last sync timestamp.

**REQ-SET-018** 🔴 P1  
**Cloud connection**: show: cloud API status, last sync timestamp, pending outbox events, failed events (with retry option).

**REQ-SET-019** 🟡 P2  
**Sync frequency**: how often the background sync worker runs (default: 30 seconds). Configurable between 10 seconds and 5 minutes.

---

## 20. Offline Sync & Connectivity

### 20.1 Connectivity States

**REQ-SYNC-001** 🔴 P1  
The application must explicitly handle three connectivity states and the user must always know which state they are in via the sync status indicator:

| State | Icon | Description |
|---|---|---|
| **A — Full** | ✅ Green | Workstation → Local Server → Cloud. Everything normal. |
| **B — Internet down** | 🟡 Amber | Workstation → Local Server only. Cloud sync queued. Pharmacy still operating. |
| **C — Local server down** | 🟠 Orange | Workstation → SQLite only. LAN sync queued. Pharmacy still operating. |

**REQ-SYNC-002** 🔴 P1  
In State B or C, all core pharmacy operations (POS, purchase entry, stock adjustment) must continue functioning **without any degradation** to the cashier's experience. No "please reconnect" blocking screens.

### 20.2 Outbox Pattern

**REQ-SYNC-003** 🔴 P1  
Every transaction must write an **outbox event** in the same database transaction as the business data. The outbox event is atomic with the business commit — they either both succeed or both fail together.

**REQ-SYNC-004** 🔴 P1  
The **Sync Worker** processes pending outbox events and posts them to the Local Server (State C→B) or Cloud (State A). The worker runs on a background thread, never blocking the UI.

**REQ-SYNC-005** 🔴 P1  
Failed sync attempts retry with **exponential backoff**: 30s → 2min → 10min → 1hr → manual retry. Maximum 10 retries before moving to FAILED status and alerting.

**REQ-SYNC-006** 🔴 P1  
**Idempotency**: every outbox event carries a `device_id + operation_id` pair. The server checks this against the `idempotency_keys` table before processing. Duplicate submissions receive the stored result without re-execution.

**AC:** Submit the same sale event twice (simulating a retry after a network timeout). The sale is recorded exactly once. Stock is deducted exactly once. Invoice number is not duplicated.

**REQ-SYNC-007** 🔴 P1  
**Conflict resolution** per entity type:

| Entity Type | Resolution |
|---|---|
| Stock balance conflict | Flag for manual reconciliation; never auto-resolve |
| Master data (product, supplier) | Server version wins |
| Price change | Server-approved version wins |
| Financial transaction | Never auto-overwrite; create correction entry |

### 20.3 Data Freshness

**REQ-SYNC-008** 🔴 P1  
**Product master** (names, prices, GST rates) is cached in workstation SQLite and refreshed on app startup and on a configurable schedule (default: 15 minutes). A "master data updated" toast is shown if changes are applied.

**REQ-SYNC-009** 🟡 P2  
**FTS5 index** is rebuilt incrementally when product master cache is refreshed. Rebuild must not block the UI or degrade search performance during the process.

---

## 21. Hardware Integration

### 21.1 Barcode Scanner

**REQ-HW-001** 🔴 P1  
USB HID barcode scanners must work **without any driver installation**. They present as keyboard devices. The app must detect the rapid burst of characters (barcode data + Enter terminator within 100ms) as a scan event vs. manual typing.

**REQ-HW-002** 🟡 P2  
Support for **serial (COM port) barcode scanners** via configurable baud rate and COM port.

**REQ-HW-003** 🟡 P2  
**Wireless (Bluetooth) scanners** that present as HID keyboards are automatically supported via REQ-HW-001.

### 21.2 Receipt Printer

**REQ-HW-004** 🔴 P1  
**Thermal receipt printers** supporting ESC/POS command set must work out of the box. Supported paper widths: 80mm (default), 58mm. Supported interfaces: USB, Serial, Network (TCP/IP).

**REQ-HW-005** 🟡 P2  
**Dot-matrix printers**: basic support for invoice printing to 9-pin/24-pin impact printers (used by older pharmacies).

**REQ-HW-006** 🟡 P2  
**Laser/InkJet (A4) printing**: GST-compliant invoice on A4 paper. Support Windows default print dialog.

**REQ-HW-007** 🟡 P2  
All printer communication is behind the `IReceiptPrinter` interface. Adding a new printer model requires only a new implementation class — no changes to business logic.

### 21.3 Cash Drawer

**REQ-HW-008** 🟡 P2  
**Cash drawer kick** via RJ11 pulse through thermal printer (standard ESC/POS mechanism). The drawer opens automatically when a cash payment is finalised.

**REQ-HW-009** 🟡 P2  
Manual cash drawer open: Ctrl+D on the POS screen (CASHIER permission required).

### 21.4 Label Printer

**REQ-HW-010** 🟢 P3  
**Barcode label printing** for products (for pharmacies that relabel their stock). Supports: Zebra/TSC ZPL/TSPL, standard thermal label printers.

---

## 22. Cloud Management Dashboard

**REQ-CLOUD-001** 🟡 P2 (Phase 8)  
ORG_OWNER can access a **web-based cloud dashboard** (separate from the desktop app) showing aggregated data across all branches.

**REQ-CLOUD-002** 🟡 P2 (Phase 8)  
Cloud dashboard must show: total daily/monthly sales across all branches, branch-wise comparison, total stock value, overdue receivables, GST liability, and sync health of each branch.

**REQ-CLOUD-003** 🟢 P3 (Phase 8)  
**Remote user management**: ORG_OWNER can create/modify users and roles for any branch from the cloud dashboard.

**REQ-CLOUD-004** 🟢 P3 (Phase 8)  
**Remote reports**: generate and download any report for any branch from the cloud dashboard without visiting that branch physically.

---

## 23. AI & Intelligence Layer (Phase 9)

> **Critical constraint:** AI is **advisory only**. AI must never autonomously execute financial transactions, modify stock balances, or post accounting entries. All AI outputs are recommendations that a human user must approve.

**REQ-AI-001** 🔵 P4 (Phase 9)  
**Demand Forecasting**: predict next 30/60/90 day demand per product using historical sales data. Accuracy target: within 15% of actual for products with ≥3 months of history.

**REQ-AI-002** 🔵 P4 (Phase 9)  
**Smart Reorder Recommendations**: generate purchase order recommendations based on: forecast demand, lead time, current stock, reorder rules. Displayed as a suggested PO — user reviews and approves.

**REQ-AI-003** 🔵 P4 (Phase 9)  
**Expiry Prediction**: flag batches at risk of expiry-before-sellout based on sales velocity. Recommend: targeted discounting, return to supplier, donate.

**REQ-AI-004** 🔵 P4 (Phase 9)  
**Sales Anomaly Detection**: alert on unusual patterns — sudden spike in a controlled substance, unusually large single sale, sale at below-cost price.

**REQ-AI-005** 🔵 P4 (Phase 9)  
**Natural Language Reporting**: allow querying reports in plain text: "Show me sales of Paracetamol last month by day." Response is a rendered chart/table.

**REQ-AI-006** 🔵 P4 (Phase 9)  
**Purchase Invoice OCR**: photograph/scan a paper purchase invoice; AI extracts: supplier, invoice number, date, line items, quantities, rates. Presented for user verification before posting. Target: 90%+ accuracy on printed invoices.

**REQ-AI-007** 🔵 P4 (Phase 9)  
**Product Deduplication**: AI flags potentially duplicate products in the master (same generic, same strength, different brand names or batch data entry inconsistencies). Suggests merge with human confirmation.

---

## 24. Platform — Update, Audit & Data

### 24.1 Application Updates

**REQ-UPD-001** 🔴 P1  
The application must check for updates **on background thread on startup** — never blocking the UI or POS access.

**REQ-UPD-002** 🔴 P1  
Updates must be delivered as **signed MSIX packages**. Code signature must be verified before installation. Corrupted or unsigned packages must be rejected.

**REQ-UPD-003** 🔴 P1  
Before applying an update, the app must:
1. Backup local SQLite database
2. Run database schema migrations
3. Validate migration success with a startup health check
4. If migration fails: rollback to previous version, alert ORG_OWNER

**REQ-UPD-004** 🟡 P2  
**Delta updates**: only changed files are downloaded, not the entire package. Reduces update download size.

### 24.2 Database Migrations

**REQ-DB-001** 🔴 P1  
All database schema changes must be managed as **numbered migration scripts** (e.g. `Migration_0001_InitialSchema.sql`). Migrations run automatically on app startup if pending. They are **never re-run** (idempotent migration tracking).

**REQ-DB-002** 🔴 P1  
**Rollback scripts** must exist for every migration that modifies existing data. Structural-only migrations (ADD COLUMN) are inherently safe; data-modifying migrations require explicit rollback.

### 24.3 Data Import / Export

**REQ-DATA-001** 🟡 P2  
**Product Master import** from CSV/Excel template. Template provided in-app. Import validates: required fields, HSN format, duplicate detection (by barcode).

**REQ-DATA-002** 🟡 P2  
**Opening stock import**: for new installations, allow bulk import of opening stock (product, batch, expiry, qty, rate) from CSV.

**REQ-DATA-003** 🟡 P2  
**MARG data import tool**: one-time import of product master, supplier master, customer master, and opening stock from MARG ERP export format (CSV/DBF). Must handle MARG's product naming conventions and data quality issues.

**REQ-DATA-004** 🟢 P3  
**Full data export**: organisation owner can export all data as encrypted ZIP (for backup, portability, or migration). Export includes: all transactions, masters, audit logs.

### 24.4 Backup & Recovery

**REQ-BKP-001** 🔴 P1  
**Local SQLite auto-backup**: daily automatic backup to a configurable local path (and optionally network path). Backup is compressed and time-stamped.

**REQ-BKP-002** 🟡 P2  
**Cloud backup**: when cloud sync is active, all data in cloud PostgreSQL is backed up with: WAL archiving (continuous), daily snapshots, off-site cross-region copy weekly.

**REQ-BKP-003** 🟡 P2  
**Restore**: restore from a backup must be performable via an in-app restore wizard (with admin authentication). Target RTO: 30 minutes.

---

## 25. Non-Functional Requirements

### 25.1 Performance (NFR-PERF)

> All targets measured on baseline hardware (4GB RAM, dual-core 2.0GHz, HDD/SATA SSD, Windows 10).

| ID | Requirement | Target | Hard Ceiling |
|---|---|---|---|
| NFR-PERF-001 | Barcode scan → product added to cart | ≤ 100ms | 200ms |
| NFR-PERF-002 | Keystroke → search results rendered | ≤ 100ms | 150ms |
| NFR-PERF-003 | Cold app start → POS ready | ≤ 3s | 5s |
| NFR-PERF-004 | Sale commit (local DB) | ≤ 50ms | 100ms |
| NFR-PERF-005 | Module navigation (warm) | ≤ 80ms | 150ms |
| NFR-PERF-006 | Module navigation (cold, first visit) | ≤ 150ms | 300ms |
| NFR-PERF-007 | Inventory grid scroll (5,000+ rows) | 60fps | No stutter |
| NFR-PERF-008 | Print trigger → receipt printing started | ≤ 300ms | 500ms |
| NFR-PERF-009 | Report generation (≤ 10,000 records) | ≤ 3s | 5s |
| NFR-PERF-010 | Report generation (≤ 100,000 records) | ≤ 10s | 20s |
| NFR-PERF-011 | Dialog open animation | ≤ 180ms | — |
| NFR-PERF-012 | PIN login completion | ≤ 500ms | — |
| NFR-PERF-013 | Command palette result | ≤ 100ms | — |

**Violation policy:** Any operation that regularly exceeds its Hard Ceiling is classified as a **P1 bug**, not a design decision.

### 25.2 Reliability & Availability (NFR-REL)

| ID | Requirement |
|---|---|
| NFR-REL-001 | Pharmacy must be able to operate during complete internet outage for ≥ 72 hours without any data loss |
| NFR-REL-002 | No sale transaction is ever lost — even if the app process is killed mid-transaction |
| NFR-REL-003 | Cloud API target availability: 99.9% uptime (8.7 hours downtime/year maximum) |
| NFR-REL-004 | Local server target availability: 99.5% (within pharmacy's control; resilient to workstation failures) |
| NFR-REL-005 | Stock balance must never go negative under any concurrency scenario |
| NFR-REL-006 | Invoice numbers must always be unique within a branch × financial year × document type scope |
| NFR-REL-007 | RPO (Recovery Point Objective): 5 minutes for cloud DB; 24 hours for local SQLite |
| NFR-REL-008 | RTO (Recovery Time Objective): 30 minutes for full cloud restore |

### 25.3 Security (NFR-SEC)

| ID | Requirement |
|---|---|
| NFR-SEC-001 | All passwords hashed with bcrypt (cost factor ≥ 12) |
| NFR-SEC-002 | All data in transit encrypted with TLS 1.2+ |
| NFR-SEC-003 | Local SQLite database encrypted using Windows DPAPI + app-level key |
| NFR-SEC-004 | Local PostgreSQL accessible only on the local LAN — never internet-facing directly |
| NFR-SEC-005 | Cloud PostgreSQL on private VNet — API is the only public-facing endpoint |
| NFR-SEC-006 | All secrets in Key Vault / Secrets Manager — never in code, config files, or repos |
| NFR-SEC-007 | org_id always derived from JWT; never trusted from client request body |
| NFR-SEC-008 | OWASP ASVS 5.0 Level 2 compliance — mandatory pre-release checklist |
| NFR-SEC-009 | NIST CSF 2.0 governance framework adopted |
| NFR-SEC-010 | SQL injection impossible — parameterised queries everywhere; no string concatenation in SQL |
| NFR-SEC-011 | API rate limiting enforced: 100 requests/minute per token (configurable) |
| NFR-SEC-012 | Vulnerability scanning in CI/CD pipeline (SAST, dependency scan, secret detection) |
| NFR-SEC-013 | All financial records immutable once posted — no soft delete, no UPDATE on posted records |

### 25.4 Scalability (NFR-SCALE)

| ID | Requirement |
|---|---|
| NFR-SCALE-001 | Single branch: support ≥ 10 concurrent cashiers without latency degradation |
| NFR-SCALE-002 | Product master: support ≥ 100,000 products without search latency degradation |
| NFR-SCALE-003 | Inventory: support ≥ 500,000 batch records without UI stutter |
| NFR-SCALE-004 | Transactions: support ≥ 5 years of transaction history (millions of records) without report latency exceeding targets |
| NFR-SCALE-005 | Cloud: support ≥ 10,000 concurrent pharmacies (multi-tenant) without cross-tenant data leakage or performance interference |

### 25.5 Accessibility (NFR-A11Y)

| ID | Requirement |
|---|---|
| NFR-A11Y-001 | WCAG 2.1 AA compliance for all user-facing screens |
| NFR-A11Y-002 | Full keyboard navigation for all screens — every action reachable without a mouse |
| NFR-A11Y-003 | All interactive elements have descriptive AutomationProperties for screen readers |
| NFR-A11Y-004 | Minimum colour contrast 4.5:1 for body text, 3:1 for large text in both themes |
| NFR-A11Y-005 | Colour is never the sole indicator of status — text label always accompanies colour |
| NFR-A11Y-006 | WinUI 3 High Contrast mode supported |
| NFR-A11Y-007 | Reduced-motion respected (system setting + in-app toggle) |
| NFR-A11Y-008 | 3 font size options (Small/Medium/Large) with graceful layout adaptation |

### 25.6 Maintainability (NFR-MAINT)

| ID | Requirement |
|---|---|
| NFR-MAINT-001 | Clean Architecture: UI layer has zero references to Infrastructure; Domain layer has zero references to any other layer |
| NFR-MAINT-002 | Hot-path code (Dapper) and cold-path code (EF Core) must never be mixed in the same repository class |
| NFR-MAINT-003 | All shortcuts are JSON config — no shortcut is hardcoded in XAML or C# code |
| NFR-MAINT-004 | All colour tokens are in ResourceDictionary — no hex value in component code |
| NFR-MAINT-005 | All tax rates are in the database — no rate is hardcoded anywhere in code |
| NFR-MAINT-006 | Unit test coverage ≥ 80% for Domain and Application layers |
| NFR-MAINT-007 | All public API endpoints must have contract tests |
| NFR-MAINT-008 | CI/CD pipeline must run all tests on every PR — no PR merged with failing tests |

### 25.7 Localisation (NFR-L10N)

| ID | Requirement |
|---|---|
| NFR-L10N-001 | All UI strings must be in resource files — no hardcoded strings in XAML or C# |
| NFR-L10N-002 | Indian number format: ₹1,00,000 (lakhs/crores) as default |
| NFR-L10N-003 | Date format: DD/MM/YYYY (Indian) as default; user-configurable |
| NFR-L10N-004 | Phase 1 language: English only. Phase 2+ languages: Hindi, Marathi, Tamil, Telugu |

---

## 26. Out of Scope

The following are explicitly **NOT** in scope for any phase of Medistock:

| Item | Reason |
|---|---|
| Mobile app (Android/iOS) for POS | Desktop-first product; mobile companion is a future product |
| Web browser-based POS | Performance requirements cannot be met in a browser for the POS workload |
| Microservices architecture from day 1 | Unnecessary operational complexity before scale demands it |
| Firebase as ERP database | Incompatible with relational accounting/inventory domain model |
| Shared SQLite file over network (SMB) | Catastrophic for concurrent pharmacy transactions |
| Cloud-only POS (no offline mode) | Violates NFR-REL-001 |
| Hardcoded GST rates | Violates maintainability and legal correctness |
| AI autonomously executing transactions | Violates product safety and financial integrity |
| Redis as source-of-truth financial database | Redis is cache — authoritative state is always PostgreSQL |
| EF Core on POS/search hot paths | Violates NFR-PERF-001, NFR-PERF-002 |
| LIKE '%query%' search | Violates NFR-PERF-002 on large product catalogues |
| Blocking the POS for any external API | Including GST, payment gateways — all are async outbox |
| `MAX(invoice_no) + 1` for numbering | Race condition under concurrent cashiers |
| Deleting financial records | All corrections via immutable reversal mechanisms |
| Acrylic/Mica on data-dense screens | Performance violation on baseline hardware |

---

*This PRD is the single authoritative requirements reference for Medistock. It must be read alongside:*  
*→ [`ARCHITECTURE.md`](./ARCHITECTURE.md) — technical implementation approach*  
*→ [`UI_DESIGN_SYSTEM.md`](./UI_DESIGN_SYSTEM.md) — design and visual specifications*  
*→ [`KEYBOARD_SHORTCUTS.md`](./KEYBOARD_SHORTCUTS.md) — input and keyboard specifications*  
*→ [`trd.md`](./trd.md) — latency performance targets*

# Medistock — Master Architecture Blueprint
> **Version:** 1.1 | **Date:** 2026-09-24 | **Status:** Active Reference  
> **Purpose:** Single authoritative document covering everything to build Medistock Pharmacy ERP Platform from scratch.


---

## TABLE OF CONTENTS
1. [Product Vision](#1-product-vision)
2. [Technology Stack (Frozen Baseline)](#2-technology-stack-frozen-baseline)
3. [System Architecture (3-Tier Topology)](#3-system-architecture-3-tier-topology)
4. [Connectivity States](#4-connectivity-states)
5. [Information Architecture & Module Map](#5-information-architecture--module-map)
6. [Database Architecture — Full ERD Domain Model](#6-database-architecture--full-erd-domain-model)
7. [Offline/Online Sync Architecture](#7-onlineoffline-sync-architecture)
8. [Backend API Architecture](#8-backend-api-architecture)
9. [POS & Core Transaction Engine](#9-pos--core-transaction-engine)
10. [Accounting Engine](#10-accounting-engine)
11. [GST & Compliance Engine](#11-gst--compliance-engine)
12. [B2B Commerce Network](#12-b2b-commerce-network)
13. [Security Architecture](#13-security-architecture)
14. [Performance Architecture (TRD Enforcement)](#14-performance-architecture-trd-enforcement)
15. [UI/UX Architecture](#15-uiux-architecture)
16. [Hardware Abstraction Layer](#16-hardware-abstraction-layer)
17. [Integration Layer](#17-integration-layer)
18. [Observability & Monitoring](#18-observability--monitoring)
19. [Deployment Architecture](#19-deployment-architecture)
20. [Development Phases (Roadmap)](#20-development-phases-roadmap)
21. [Testing Strategy](#21-testing-strategy)
22. [What NOT to Build](#22-what-not-to-build)

---

## 1. Product Vision

**Do not build:** "MARG with a nicer UI."  
**Build:** An offline-first, multi-branch pharmacy operating system with integrated accounting, procurement, B2B pharmacy-to-wholesaler commerce, inventory intelligence, compliance, and cloud management.

### Five Systems Sharing One Platform

```
┌─────────────────────────────────────────────────────────────┐
│                  MEDISTOCK PLATFORM                         │
├─────────────────────────────────────────────────────────────┤
│  1. STORE ERP                                               │
│     POS • Inventory • Purchase • Sales • Accounting         │
│                                                             │
│  2. PHARMACY OPERATIONS                                     │
│     Batch • Expiry • Prescription • Patient • Doctor        │
│                                                             │
│  3. B2B NETWORK                                             │
│     Pharmacy ↔ Wholesaler ↔ Distributor                     │
│                                                             │
│  4. MANAGEMENT CLOUD                                        │
│     Multi-branch • Analytics • Reports • Users              │
│                                                             │
│  5. PLATFORM LAYER                                          │
│     Identity • Security • Sync • Audit • Updates            │
└─────────────────────────────────────────────────────────────┘
```

### Competitive Moat
The moat is not the UI. It is the combination of:
- Offline-first local operation
- Multi-user LAN concurrency with shared local PostgreSQL
- Cloud synchronization via outbox pattern
- Proper double-entry accounting built-in
- Pharmacy-specific inventory (batch/expiry/schedule)
- GST + e-Invoice + e-Way bill compliance
- B2B transaction network (pharmacy ↔ wholesaler ↔ distributor)
- Multi-branch management
- Immutable audit trail
- Reliable distributed transactions

---

## 2. Technology Stack (Frozen Baseline)

| Layer | Technology | Notes |
|---|---|---|
| **Windows Client** | C# / .NET 10 LTS | LTS until Nov 2028 |
| **Desktop UI** | WinUI 3 + Windows App SDK | MS recommended for new native Windows apps |
| **Client Architecture** | MVVM + Clean Architecture | Separation of concerns |
| **Local Client DB** | SQLite (WAL mode) | Per-workstation cache + offline queue |
| **Local Server** | ASP.NET Core minimal API | Runs on local pharmacy server machine |
| **Local Server DB** | PostgreSQL | Authoritative operational DB per branch |
| **Cloud API** | ASP.NET Core | Modular monolith → microservices when warranted |
| **Cloud DB** | PostgreSQL | With read replicas + PITR backups |
| **Cache** | Redis | Sessions, permissions, master data, rate limits |
| **Object Storage** | Azure Blob / S3-compatible | Images, PDFs, backups |
| **Hot Path Data Access** | **Dapper / Raw ADO.NET** | POS, search, cart, stock commits — NO EF Core |
| **Cold Path Data Access** | **EF Core** | Admin, reports, settings, accounting |
| **Product Search Index** | **SQLite FTS5 shadow table** | Updated via triggers, never rebuilt at query time |
| **Auth** | OIDC / OAuth 2.1 | Short-lived tokens, rotating refresh tokens |
| **MFA** | TOTP / WebAuthn | Based on deployment tier |
| **Secrets** | Azure Key Vault / AWS Secrets Manager | Never in source code |
| **Logging** | Serilog | Structured logs |
| **Observability** | OpenTelemetry | Traces, metrics, logs |
| **Messaging (initial)** | PostgreSQL Outbox Pattern | No premature infrastructure |
| **Messaging (at scale)** | Azure Service Bus / RabbitMQ / Kafka | Adopt when load demands |
| **CI/CD** | GitHub Actions / Azure DevOps | Full pipeline with SAST, secret scan |
| **IaC** | Terraform | Reproducible infrastructure |
| **Containers** | Docker | For cloud services |
| **WAF** | Cloudflare / Azure Front Door | DDoS, WAF |
| **Desktop Deployment** | Signed MSIX installer | Code-signed, auto-updater |
| **Security Baseline** | OWASP ASVS 5.0 | Formal verification checklist |
| **Security Governance** | NIST CSF 2.0 | Organizational risk framework |

---

## 3. System Architecture (3-Tier Topology)

```
                     ┌──────────────────────────┐
                     │       CLOUD PLATFORM     │
                     │  ┌──────┐ ┌───────────┐  │
                     │  │ ID   │ │ B2B Net   │  │
                     │  │ Auth │ │ Notif     │  │
                     │  │ ERP  │ │ Reporting │  │
                     │  │ Sync │ │ Billing   │  │
                     │  └──────┘ └───────────┘  │
                     └────────────┬─────────────┘
                                  │
              ┌──────────┬────────┴──────────┬──────────┐
              │          │                   │          │
         PostgreSQL    Redis          Object Storage  WAF/CDN
         (+ replicas)

══════════════════════════════════════════════════════════════
                    PHARMACY / BRANCH (LAN)
══════════════════════════════════════════════════════════════

              ┌─────────────────────────────┐
              │     Local Pharmacy Server   │
              │  ASP.NET Core Local API     │
              │  PostgreSQL (operational)   │
              │  Sync Engine (outbox)       │
              └────────────┬────────────────┘
                           │ LAN
          ┌────────────────┼──────────────────┐
          │                │                  │
       Cashier 1       Cashier 2          Pharmacist
          │                │                  │
   WinUI 3 + SQLite  WinUI 3 + SQLite  WinUI 3 + SQLite
   (cache + queue)   (cache + queue)   (cache + queue)
```

### Why Two Local Database Levels

**Per-workstation SQLite (WinUI app):**
- Cached product master data (FTS5 indexed)
- UI state & offline queue (outbox events)
- Pending operations when local server is unreachable
- Local configuration & session data
- Temporary documents (draft sales, hold bills)

**Pharmacy local PostgreSQL server:**
- **Authoritative operational source of truth for that branch**
- Handles multi-counter concurrent stock deductions atomically
- Runs full transaction isolation for sale commits
- LAN-accessible to all workstations

---

## 4. Connectivity States

The application must explicitly understand and handle three states:

### State A — Full Connectivity
```
Workstation → Local Server → Local PostgreSQL → Cloud Sync → Cloud
```
Everything synchronizes normally in real-time.

### State B — Internet Down (LAN only)
```
Workstation → Local Server → Local PostgreSQL
                                    ↓
                            [Cloud Sync Pending Outbox]
```
Pharmacy continues operating. All transactions are committed locally. Outbox queue drains when internet returns.

### State C — Local Server Down (workstation isolated)
```
Workstation → SQLite → Local Outbox Queue
```
When server returns:
```
SQLite Outbox → Local PostgreSQL → Cloud Outbox → Cloud
```
This is genuine resilience. The workstation continues accepting transactions.

---

## 5. Information Architecture & Module Map

### Full Module Hierarchy

```
MEDISTOCK
│
├── ORGANIZATION MANAGEMENT
│   ├── Company / Tenant Setup
│   ├── Branch Management
│   ├── Warehouse / Location
│   ├── Counter / POS Terminal
│   └── Financial Year / Period
│
├── IDENTITY & ACCESS
│   ├── Users (Admin, Owner, Manager, Pharmacist, Cashier, Accountant, Storekeeper, Procurement, Delivery)
│   ├── Roles (dynamic, not hardcoded)
│   ├── Permissions (granular: SALE_CREATE, SALE_CANCEL, PRICE_CHANGE, DISCOUNT_APPROVE, etc.)
│   └── Contextual Policies (Cashier: discount ≤5%, Manager: ≤20%, Owner: unlimited)
│
├── PRODUCT MASTER
│   ├── Product Identity (name, brand, generic, salt, manufacturer)
│   ├── Composition & Strength
│   ├── Dosage Form (tablet, syrup, injection, cream, etc.)
│   ├── Pack Size & Unit (strip/tablet conversion)
│   ├── HSN Code & GST Classification
│   ├── Drug Schedule (OTC, H, H1, Narcotic/restricted)
│   ├── Barcodes (multiple per product)
│   ├── Alternative Codes (SKU, manufacturer code)
│   ├── Purchase / Sales Configuration
│   ├── Reorder Rules (min, max, lead time, safety stock)
│   └── Regulatory Flags (cold-chain, prescription required)
│
├── INVENTORY
│   ├── Batch Management (batch no, expiry, MRP, purchase rate, sale rate, qty, reserved qty)
│   ├── Warehouse / Location / Rack
│   ├── Stock Ledger (movement audit trail: PURCHASE/SALE/RETURN/TRANSFER/DAMAGE/EXPIRY/ADJUSTMENT)
│   ├── Stock Reservations
│   ├── Stock Transfers (inter-branch with state machine)
│   ├── Expiry Dashboard (30/60/90 day bands + ₹value at risk)
│   ├── Low Stock / Out of Stock Alerts
│   └── Intelligent Reorder (avg daily sales × lead time + safety stock)
│
├── POS & SALES
│   ├── POS Terminal (barcode-first, keyboard-first)
│   ├── Product Fast Search (FTS5, ≤100ms)
│   ├── Cart / Bill Management
│   ├── Hold Bill / Resume Bill
│   ├── Discount Engine (item-level, bill-level, scheme-based)
│   ├── Scheme / Promotion Engine (buy X get Y, %, free qty)
│   ├── Payment (Cash, Card, UPI, Credit, Split)
│   ├── GST Calculation (auto CGST/SGST vs IGST)
│   ├── Invoice Generation & Print
│   ├── Sales Return / Credit Note
│   ├── Prescription Management (patient, doctor, Schedule-H gate)
│   └── Customer Loyalty (points, schemes, reminders)
│
├── PURCHASE & PROCUREMENT
│   ├── Purchase Requisition
│   ├── Supplier Quotation
│   ├── Purchase Order
│   ├── Goods Receipt Note (GRN)
│   ├── Purchase Invoice (batch entry, expiry, HSN, GST)
│   ├── Purchase Return / Debit Note
│   ├── Accounts Payable
│   └── Supplier Payment
│
├── ACCOUNTING
│   ├── Chart of Accounts (Assets / Liabilities / Equity / Revenue / Expenses)
│   ├── Journal & Journal Lines (double-entry, immutable once posted)
│   ├── Accounts Receivable
│   ├── Accounts Payable
│   ├── Cash & Bank
│   ├── Payment Allocation
│   ├── Credit Note / Debit Note
│   ├── Bank Reconciliation
│   ├── Trial Balance
│   ├── P&L Statement
│   ├── Balance Sheet
│   └── Cash Flow
│
├── GST & COMPLIANCE
│   ├── GSTIN / HSN Management
│   ├── Tax Rate Rules (versioned, configurable)
│   ├── CGST / SGST / IGST Auto-calculation
│   ├── Input Tax Credit (ITC)
│   ├── E-Invoice (JSON export, IRN)
│   ├── E-Way Bill
│   ├── GSTR-1 / GSTR-2 / GSTR-3B Generation
│   ├── TDS on Purchases
│   └── Audit & Document Retention (6-year rule)
│
├── B2B COMMERCE NETWORK
│   ├── Purchase Request (pharmacy → wholesaler)
│   ├── Quotation / Acceptance
│   ├── Purchase Order / Sales Order (linked B2B transaction)
│   ├── Pick List & Warehouse Picking
│   ├── Packing & Package QR
│   ├── Fulfillment (Delivery / Pickup / Courier)
│   ├── OTP / QR Pickup Verification
│   ├── Sales Invoice (wholesaler side)
│   ├── Goods Receiving & Discrepancy Handling
│   ├── Purchase Invoice (pharmacy side)
│   ├── Discrepancy / Dispute Management
│   ├── B2B Returns (Return Request → Credit Note → Inventory Adjustment)
│   ├── B2B Transaction Timeline (unified status view)
│   ├── Credit Limit Management
│   └── B2B Marketplace (compare suppliers, price, availability, terms)
│
├── MULTI-BRANCH
│   ├── Branch Dashboard & Consolidated Reporting
│   ├── Inter-Branch Stock Transfer (with approval workflow)
│   ├── Central Product Master
│   └── Branch-level Permission Isolation
│
├── CRM & PATIENTS
│   ├── Customer Profiles
│   ├── Patient Records
│   ├── Doctor Registry
│   ├── Prescription History
│   ├── Loyalty Points & Schemes
│   └── Automated Reminders (prescription refill, SMS/WhatsApp/Email)
│
├── REPORTS & ANALYTICS
│   ├── Sales Reports (by product, category, customer, doctor, payment, date, branch)
│   ├── Purchase Reports (by supplier, product, GST)
│   ├── Inventory Reports (stock summary, batch-wise, expiry, movement)
│   ├── Profitability Reports
│   ├── Financial Reports (trial balance, P&L, balance sheet, cash flow)
│   ├── GST Reports (GSTR-1/2/3B)
│   ├── Audit Reports
│   └── Role-based Dashboard (Owner / Manager / Pharmacist / Accountant views)
│
├── AI LAYER (Phase 9)
│   ├── Demand Forecasting
│   ├── Smart Reorder Recommendations
│   ├── Expiry Prediction
│   ├── Sales Anomaly Detection
│   ├── Natural-Language Reporting
│   ├── Invoice OCR (purchase entry automation)
│   ├── Product Matching (deduplication)
│   └── Support Assistant
│
└── PLATFORM
    ├── Offline Sync Engine (Outbox pattern)
    ├── Update System (signed MSIX, auto-updater)
    ├── Database Migration Engine (versioned)
    ├── Feature Flags
    ├── Notification Service (In-App / Push / SMS / Email / WhatsApp)
    ├── Workflow / Approval Engine (configurable)
    ├── Audit Log (immutable: WHO, WHAT, WHEN, WHERE, BEFORE, AFTER)
    └── Data Export / Import (CSV, Excel, JSON, PDF)
```

---

## 6. Database Architecture — Full ERD Domain Model

### 6.1 Core Entities (All Tables)

```
── ORGANIZATION ──────────────────────────────────────────────
  organizations          (id, name, gstin, type, plan, settings)
  branches               (id, org_id, name, address, gstin, is_active)
  warehouses             (id, branch_id, name, type)
  locations              (id, warehouse_id, rack, shelf, bin)
  financial_years        (id, org_id, start_date, end_date, is_active)
  counters               (id, branch_id, name, is_active)
  devices                (id, branch_id, name, type, last_seen_at)

── IDENTITY & ACCESS ─────────────────────────────────────────
  users                  (id, org_id, name, email, phone, pin_hash, is_active)
  roles                  (id, org_id, name, description)
  permissions            (id, code, description)  -- e.g. SALE_CREATE
  role_permissions       (role_id, permission_id, conditions_json)
  user_roles             (user_id, role_id, branch_id)
  sessions               (id, user_id, device_id, token_hash, expires_at)

── PRODUCT MASTER ────────────────────────────────────────────
  manufacturers          (id, org_id, name, gstin, is_global)
  salts                  (id, name, description)  -- generic/composition
  product_categories     (id, org_id, parent_id, name, hsn_code)
  units                  (id, name, abbreviation)
  products               (id, org_id, name, brand_name, generic_name,
                           manufacturer_id, composition, strength, dosage_form,
                           pack_size, unit_id, strip_size, base_unit,
                           hsn_code, gst_rate_id, schedule, is_prescription_req,
                           is_cold_chain, is_narcotic, is_active, global_product_id)
  product_salts          (product_id, salt_id, strength)
  product_barcodes       (id, product_id, barcode, type)
  product_pricing        (id, product_id, branch_id, mrp, purchase_rate, sale_rate,
                           wholesale_rate, effective_from, effective_to)
  product_reorder_rules  (id, product_id, warehouse_id, min_stock, max_stock,
                           reorder_point, reorder_qty, lead_days)
  fts_products           (VIRTUAL TABLE — FTS5: rowid=product_id, content: name+generic+brand+salt+barcode+manufacturer)

── INVENTORY ─────────────────────────────────────────────────
  batches                (id, product_id, org_id, batch_no, expiry_date,
                           mfg_date, mrp, purchase_rate, sale_rate)
  stock_balances         (id, batch_id, warehouse_id, location_id,
                           quantity, reserved_quantity, available_quantity)
  stock_movements        (id, org_id, branch_id, warehouse_id, batch_id,
                           movement_type, quantity, reference_type, reference_id,
                           unit_cost, user_id, device_id, created_at)
                          -- movement_type: PURCHASE/SALE/SALE_RETURN/PURCHASE_RETURN
                          --   STOCK_TRANSFER/ADJUSTMENT/DAMAGE/EXPIRY/OPENING/CORRECTION
  stock_reservations     (id, batch_id, warehouse_id, quantity, reference_type,
                           reference_id, expires_at, status)
  stock_transfers        (id, org_id, from_branch_id, to_branch_id, status,
                           requested_by, approved_by, created_at, completed_at)
  stock_transfer_items   (id, transfer_id, batch_id, requested_qty, approved_qty,
                           picked_qty, received_qty)

── SUPPLIERS & CUSTOMERS ────────────────────────────────────
  suppliers              (id, org_id, name, gstin, dl_number, address, phone,
                           email, credit_limit, credit_days, outstanding, is_active)
  customers              (id, org_id, name, phone, email, gstin, address,
                           loyalty_points, credit_limit, outstanding, is_active)
  doctors                (id, org_id, name, registration_no, specialization, phone)
  patients               (id, org_id, name, dob, gender, phone, doctor_id)

── SALES ─────────────────────────────────────────────────────
  sales                  (id, ulid, org_id, branch_id, counter_id, warehouse_id,
                           customer_id, patient_id, doctor_id, user_id, device_id,
                           invoice_no, invoice_date, status, subtotal, discount_amt,
                           tax_amt, round_off, total, payment_status,
                           prescription_ref, notes, created_at, posted_at)
  sale_items             (id, sale_id, product_id, batch_id, quantity, unit_price,
                           mrp, discount_pct, discount_amt, taxable_amt,
                           cgst_rate, cgst_amt, sgst_rate, sgst_amt,
                           igst_rate, igst_amt, net_amt, strip_qty, tablet_qty)
  sale_payments          (id, sale_id, payment_mode, amount, reference, paid_at)
  sale_returns           (id, org_id, branch_id, original_sale_id, user_id,
                           return_date, reason, credit_note_no, total_amt,
                           status, created_at)
  sale_return_items      (id, return_id, sale_item_id, batch_id, quantity,
                           restock_decision, reason)
  held_bills             (id, org_id, counter_id, user_id, bill_data_json,
                           held_at, released_at)

── PURCHASE ──────────────────────────────────────────────────
  purchase_requests      (id, org_id, branch_id, warehouse_id, user_id, status,
                           supplier_id, notes, created_at)
  purchase_request_items (id, request_id, product_id, requested_qty, approved_qty)
  purchase_orders        (id, org_id, branch_id, supplier_id, user_id,
                           po_no, po_date, expected_date, status, total_amt)
  purchase_order_items   (id, po_id, product_id, ordered_qty, unit_price, total)
  goods_receipt_notes    (id, org_id, branch_id, po_id, warehouse_id, user_id,
                           grn_no, grn_date, status)
  grn_items              (id, grn_id, product_id, batch_id, ordered_qty,
                           received_qty, unit_price, expiry_date, mfg_date)
  purchase_invoices      (id, ulid, org_id, branch_id, supplier_id, grn_id,
                           invoice_no, supplier_invoice_no, invoice_date,
                           subtotal, discount_amt, tax_amt, total,
                           payment_status, status, created_at, posted_at)
  purchase_invoice_items (id, invoice_id, product_id, batch_id, quantity,
                           unit_price, mrp, discount_pct, taxable_amt,
                           cgst_rate, cgst_amt, sgst_rate, sgst_amt,
                           igst_rate, igst_amt, net_amt)
  purchase_returns       (id, org_id, branch_id, invoice_id, supplier_id,
                           user_id, return_date, debit_note_no, reason,
                           total_amt, status)
  purchase_return_items  (id, return_id, invoice_item_id, batch_id, quantity, reason)

── ACCOUNTING ────────────────────────────────────────────────
  accounts               (id, org_id, code, name, type, parent_id, is_system,
                           is_active)
                          -- types: ASSET/LIABILITY/EQUITY/REVENUE/EXPENSE
  journals               (id, org_id, branch_id, financial_year_id,
                           journal_no, journal_date, narration, reference_type,
                           reference_id, status, total_debit, total_credit,
                           created_by, posted_at)
  journal_lines          (id, journal_id, account_id, debit, credit, narration)
  ledger_entries         (id, org_id, account_id, branch_id, financial_year_id,
                           journal_id, journal_line_id, entry_date, debit, credit,
                           balance, reference)
  payments               (id, org_id, branch_id, payment_type, party_type, party_id,
                           payment_mode, amount, reference_no, payment_date,
                           bank_account, status, journal_id)
  payment_allocations    (id, payment_id, invoice_type, invoice_id, allocated_amt)

── GST & COMPLIANCE ──────────────────────────────────────────
  gst_rates              (id, rate_pct, cgst_pct, sgst_pct, igst_pct,
                           hsn_range, effective_from, effective_to)
  gst_transactions       (id, org_id, branch_id, transaction_type, reference_id,
                           gstin_buyer, gstin_seller, invoice_no, invoice_date,
                           taxable_value, cgst, sgst, igst, total_tax,
                           place_of_supply, is_interstate, irn, eway_bill_no)
  e_invoices             (id, org_id, gst_transaction_id, irn, ack_no, ack_date,
                           qr_code, signed_invoice_json, status)
  eway_bills             (id, org_id, gst_transaction_id, ewb_no, ewb_date,
                           vehicle_no, transporter, status)
  gstr_returns           (id, org_id, branch_id, return_type, period,
                           status, filed_at, data_json)

── B2B TRANSACTIONS ──────────────────────────────────────────
  b2b_transactions       (id, ulid, buyer_org_id, seller_org_id, status,
                           created_at, completed_at)
                          -- status: DRAFT/SUBMITTED/ACCEPTED/CONFIRMED/PICKING/
                          --   PACKED/READY_FOR_PICKUP/HANDED_OVER/IN_TRANSIT/
                          --   DELIVERED/RECEIVED/INVOICED/COMPLETED/
                          --   REJECTED/CANCELLED/PARTIALLY_ACCEPTED/
                          --   PARTIALLY_RECEIVED/DISPUTED/RETURN_REQUESTED/RETURNED
  b2b_transaction_events (id, b2b_txn_id, event_type, actor_org_id, actor_user_id,
                           payload_json, occurred_at)
  b2b_purchase_requests  (id, b2b_txn_id, buyer_org_id, seller_org_id,
                           status, notes, created_at)
  b2b_pr_items           (id, pr_id, product_id, requested_qty, accepted_qty,
                           unit_price, total)
  b2b_sales_orders       (id, b2b_txn_id, seller_org_id, so_no, status,
                           fulfillment_method, created_at)
                          -- fulfillment_method: DELIVERY/PICKUP/COURIER
  b2b_so_items           (id, so_id, product_id, batch_id, quantity, unit_price)
  b2b_pick_lists         (id, so_id, warehouse_id, assigned_to, status, created_at)
  b2b_pick_list_items    (id, pick_list_id, product_id, batch_id, requested_qty,
                           picked_qty, location_id)
  b2b_packages           (id, so_id, package_code, qr_code, weight_kg,
                           packed_at, packed_by)
  b2b_package_items      (id, package_id, batch_id, product_id, quantity)
  b2b_deliveries         (id, b2b_txn_id, package_id, method, status,
                           delivery_agent, vehicle_no, dispatched_at,
                           delivered_at, proof_of_delivery)
  b2b_pickups            (id, b2b_txn_id, package_id, pickup_person_name,
                           pickup_person_phone, otp_code, otp_verified,
                           collected_at, collected_by_user_id)
  b2b_sales_invoices     (id, b2b_txn_id, seller_org_id, si_no, invoice_date,
                           subtotal, tax_amt, total, status)
  b2b_purchase_receipts  (id, b2b_txn_id, buyer_org_id, warehouse_id,
                           received_by, received_at, status)
  b2b_receipt_items      (id, receipt_id, product_id, batch_id,
                           expected_qty, received_qty, discrepancy_qty, notes)
  b2b_discrepancies      (id, b2b_txn_id, receipt_id, status, resolution)
  b2b_purchase_invoices  (id, b2b_txn_id, buyer_org_id, pi_no, si_reference,
                           invoice_date, total, status)

── PROMOTIONS & PRICING ─────────────────────────────────────
  promotion_rules        (id, org_id, name, type, condition_json, action_json,
                           priority, effective_from, effective_to, is_active)
                          -- type: BUY_X_GET_Y / PERCENTAGE_DISCOUNT / FREE_QTY
  customer_price_lists   (id, org_id, customer_id, product_id, price,
                           effective_from, effective_to)

── SYNC / PLATFORM ──────────────────────────────────────────
  outbox_events          (id, aggregate_type, aggregate_id, event_type, payload_json,
                           created_at, status, retry_count, synced_at, error_msg)
                          -- status: PENDING/SYNCED/FAILED
  sync_checkpoints       (id, device_id, entity_type, last_sync_at, last_seq)
  idempotency_keys       (id, device_id, operation_id, resolved_at, result_json)

── AUDIT & NOTIFICATIONS ────────────────────────────────────
  audit_logs             (id, org_id, branch_id, user_id, device_id, action,
                           entity_type, entity_id, old_value_json, new_value_json,
                           reason, approved_by, ip_address, occurred_at)
  business_events        (id, org_id, event_type, payload_json, occurred_at)
  notifications          (id, org_id, user_id, channel, event_type, content,
                           status, sent_at, read_at)

── DOCUMENTS ─────────────────────────────────────────────────
  prescriptions          (id, org_id, patient_id, doctor_id, issued_date,
                           expiry_date, prescription_no, scan_url, status)
  prescription_items     (id, prescription_id, product_id, dosage, duration, qty)
  document_sequences     (id, org_id, branch_id, doc_type, prefix, last_no,
                           financial_year_id)
                          -- Handles INV-2026-000182 style numbering atomically
```

---

## 7. Online/Offline Sync Architecture

### 7.1 Outbox Pattern (Core)

Every transaction on local DB writes an outbox event atomically in the same transaction:

```
BEGIN TRANSACTION
  1. Commit sale to sales / sale_items / sale_payments
  2. Deduct stock in stock_balances
  3. Create stock_movements record
  4. Create accounting journal entries
  5. Write outbox_event (status=PENDING)
COMMIT
```

A background **Sync Worker** processes the outbox:
```
Outbox Event (PENDING)
    ↓
Sync Worker reads event
    ↓
HTTP POST to Cloud API
    ↓
Idempotency check (device_id + operation_id)
    ↓
Server validates + commits
    ↓
Server returns ACK
    ↓
outbox_event.status = SYNCED
```

On failure (cloud 5xx, timeout):
- `status` stays `PENDING`, `retry_count++`
- Exponential backoff retry (30s → 2min → 10min → 1hr)
- Transaction is **never lost**

### 7.2 Conflict Resolution Rules

| Conflict Type | Resolution Strategy |
|---|---|
| Stock conflict | Explicit reconciliation required; never auto-resolve |
| Price conflict | Server policy wins; local cache refreshed |
| Master data conflict | Latest server-approved version wins |
| Financial transaction | Never silently overwrite; create correction entry |
| Configuration conflict | Server version wins |

### 7.3 Idempotency

Every outbox event carries:
```json
{
  "device_id": "PC-BRANCH-A-01",
  "operation_id": "01J8KX...",
  "event_type": "SALE_COMMITTED",
  "payload": { ... }
}
```

Server checks `idempotency_keys` before processing. Duplicate receives the same stored result without re-execution.

### 7.4 Document Numbers & Sequence Safety

**Never:** `MAX(invoice_no) + 1` — race condition between two cashiers.  
**Use:** `document_sequences` table with `SELECT ... FOR UPDATE` (PostgreSQL advisory locks or row locks).

Format: `INV-2026-000182` (human-readable)  
Internal ID: `ULID` (sortable, distributed-safe)

---

## 8. Backend API Architecture

### 8.1 API Modules (Modular Monolith — split only when justified)

```
/api/v1/
├── auth/          (login, refresh, logout, MFA, devices)
├── identity/      (users, roles, permissions, organizations)
├── products/      (CRUD, search, FTS5, barcodes, pricing)
├── inventory/     (stock, batches, movements, transfers)
├── sales/         (POS, cart, invoice, returns, payments)
├── purchases/     (requisitions, POs, GRN, invoices, returns)
├── accounting/    (journals, ledger, accounts, reports)
├── gst/           (calculations, e-invoice, eway-bill, GSTR)
├── b2b/           (requests, orders, fulfillment, invoices, receipts)
├── customers/     (CRM, patients, doctors, loyalty)
├── suppliers/     (master, credit, payments)
├── reports/       (parameterized, downloadable)
├── notifications/ (send, preferences, history)
├── sync/          (device registration, checkpoint, outbox drain)
├── admin/         (settings, feature-flags, migrations)
└── webhooks/      (GST portal callbacks, payment gateway)
```

### 8.2 Request/Response Standards

- **Auth:** Bearer JWT (access token: 15min, refresh: 7d rotating)
- **Tenant context:** Derived from JWT claims, never from request body
- **Pagination:** Cursor-based for large lists (no OFFSET on big tables)
- **Error format:** RFC 9457 Problem Details
- **Versioning:** `/api/v1/` prefix; additive changes are non-breaking
- **OpenAPI spec:** Generated and published; clients auto-generated where practical

### 8.3 Hot Path vs Cold Path (Backend)

| Path | Endpoints | Data Access | Target Latency |
|---|---|---|---|
| **Hot** | `/sales/commit`, `/products/search`, `/stock/deduct` | Dapper + raw SQL | < 50ms |
| **Cold** | `/reports/*`, `/accounting/*`, `/admin/*` | EF Core | < 2s acceptable |

---

## 9. POS & Core Transaction Engine

### 9.1 Keyboard & Barcode Shortcuts

| Key | Action |
|---|---|
| F2 | New Sale |
| F3 | Product Search |
| F4 | Apply Discount |
| F5 | Hold Bill |
| F6 | Payment Screen |
| F7 | Sale Return |
| F8 | Customer Lookup |
| F9 | Print Invoice |
| Ctrl+K | Command Palette (global search anything) |
| ESC | Cancel / Back |

### 9.2 Sale Transaction Flow (Atomic)

```sql
BEGIN TRANSACTION
  1. Validate user session + SALE_CREATE permission
  2. Validate product + batch (not expired, not restricted without prescription)
  3. Validate stock: UPDATE stock_balances SET quantity = quantity - @qty
                     WHERE batch_id = @batch AND quantity >= @qty
     → 0 rows affected = insufficient stock (ROLLBACK)
  4. Create sale record (status=DRAFT → POSTED)
  5. Create sale_items records
  6. Create sale_payments records
  7. Create stock_movements record (type=SALE)
  8. Create journal + journal_lines (Dr Cash/Receivable, Cr Sales, Cr GST Payable)
  9. Create audit_log entry
  10. Write outbox_event (status=PENDING)
COMMIT
```

**Rule:** If step 10 fails, rollback all. If committed but sync fails later, outbox handles retry.

### 9.3 Pharmacy-Specific Sale Controls

| Condition | Enforcement |
|---|---|
| Schedule H / H1 drug | Require prescription reference; block sale without it |
| Narcotic | Block sale if doctor registration not on file; log mandatorily |
| Cold-chain item | Display warning; require acknowledgment |
| Quantity restriction | Block if qty exceeds maximum single-sale limit |
| Expired batch | Hard block — cannot add to cart |

### 9.4 POS Search (FTS5 — Sub-100ms)

```sql
-- FTS5 shadow table kept in sync via SQLite trigger on products
SELECT p.id, p.name, p.brand_name, p.generic_name, p.mrp, 
       sb.available_quantity, pb.barcode
FROM fts_products fp
JOIN products p ON p.id = fp.rowid
JOIN stock_balances sb ON sb.product_id = p.id
WHERE fts_products MATCH @query
LIMIT 20;
```

- Debounce: 150ms before firing
- Cancel in-flight query when next keystroke arrives (CancellationToken)
- Never hits cloud/network — SQLite local only

---

## 10. Accounting Engine

### 10.1 Double-Entry Rules (Auto-Journal)

| Transaction | Debit | Credit |
|---|---|---|
| Cash Sale | Cash | Sales Revenue + GST Payable |
| Credit Sale | Customer Receivable | Sales Revenue + GST Payable |
| Cash Purchase | Inventory + Input GST | Cash |
| Credit Purchase | Inventory + Input GST | Supplier Payable |
| Payment from Customer | Cash | Customer Receivable |
| Payment to Supplier | Supplier Payable | Cash |
| Sale Return | Sales Revenue + GST Payable | Cash / Customer Receivable |

### 10.2 Financial Immutability Rule

- **DRAFT** → editable freely
- **POSTED** → immutable; corrections via Credit Note / Debit Note / Reversal journal only
- **Never:** `UPDATE sales SET total = ...` after posting

---

## 11. GST & Compliance Engine

### 11.1 GST Calculation Logic

```
Determine supply type:
  buyer GSTIN state == seller GSTIN state → CGST + SGST
  buyer GSTIN state != seller GSTIN state → IGST
  buyer has no GSTIN (retail) → CGST + SGST (same state assumed)

Look up gst_rates by product.hsn_code and effective date
Apply: taxable_value × rate / 100

All rules are versioned — never hardcode rates
```

### 11.2 External APIs (Async — Never Block Sale)

```
Sale committed locally (synchronous, ≤50ms)
         ↓
outbox_event: E_INVOICE_REQUIRED
         ↓
Background worker → GST E-Invoice API → IRN + QR
         ↓
Update sale with IRN (async)
```

If GST API is down: pharmacy still sells. IRN is fetched and backfilled when API recovers.

---

## 12. B2B Commerce Network

### 12.1 B2B Transaction State Machine

```
DRAFT → SUBMITTED → ACCEPTED → CONFIRMED → PICKING → PACKED
→ READY_FOR_PICKUP → HANDED_OVER → IN_TRANSIT → DELIVERED
→ RECEIVED → INVOICED → COMPLETED

Exception states:
REJECTED | CANCELLED | PARTIALLY_ACCEPTED | PARTIALLY_RECEIVED | DISPUTED |
RETURN_REQUESTED | RETURNED
```

### 12.2 Pickup Verification Flow

```
Order: READY_FOR_PICKUP
    ↓
Pharmacy sends "Rahul" to collect
    ↓
Wholesaler ERP: [SCAN PACKAGE QR] + [Verify OTP or employee QR]
    ↓
b2b_pickups.otp_verified = true
    ↓
Package status: HANDED_OVER
    ↓
Sales Invoice auto-generated
    ↓
Event pushed to pharmacy ERP
```

### 12.3 Receiving Discrepancy Handling

```
Expected: Amoxicillin × 50
Received: Amoxicillin × 47

→ Create b2b_discrepancies record
→ Receive 47 units into inventory
→ Alert seller ERP: "3 units missing on SO-00891"
→ Seller investigates
→ Resolution: partial credit note OR future delivery
→ NEVER silently accept incorrect quantity
```

---

## 13. Security Architecture

### 13.1 Layered Model

```
Request
  ↓ TLS
  ↓ WAF (rate limit, geo-block, DDoS)
  ↓ API Gateway
  ↓ Authentication (JWT validation)
  ↓ Authorization (RBAC + ABAC policy)
  ↓ Tenant Isolation (org_id derived from JWT, never from body)
  ↓ Input Validation (FluentValidation)
  ↓ Business Rules
  ↓ Database Constraints (FK, CHECK, NOT NULL)
  ↓ Audit Log
  ↓ Response
```

### 13.2 Authorization Examples

```
Permission: DISCOUNT_APPROVE
Cashier:    discount ≤ 5%  → allowed
Manager:    discount ≤ 20% → allowed
Owner:      no limit       → allowed

Permission: SALE_RETURN
Return > ₹5,000            → require manager approval workflow
```

### 13.3 Tenant Isolation Rule

**Every database query** that accesses business data MUST include `AND org_id = @org_id`.  
The `org_id` is ALWAYS derived from the authenticated JWT — never trusted from the request payload.

### 13.4 Key Security Policies

- Short-lived access tokens (15 min), rotating refresh tokens
- MFA enforced for Owner / Admin roles
- Local SQLite database encrypted (Windows DPAPI + app-level key)
- Secrets in Key Vault / Secrets Manager (never in code or config files)
- All external DB connections over TLS
- PostgreSQL on private network (never internet-facing directly)
- OWASP ASVS 5.0 as formal acceptance checklist before each release

---

## 14. Performance Architecture (TRD Enforcement)

### 14.1 Hard Latency Targets (Baseline Rig: 4GB RAM, dual-core, HDD/SATA SSD)

| Operation | Target | Hard Ceiling | Enforcement |
|---|---|---|---|
| Barcode scan → cart | ≤ 100ms | 200ms | Local SQLite only; async pipeline |
| Keystroke → search results | ≤ 100ms | 150ms | FTS5 + 150ms debounce + cancellation |
| Cold start → POS ready | ≤ 3s | 5s | Lazy-load all non-POS modules |
| Sale finalize → DB commit | ≤ 50ms | 100ms | Local commit; cloud sync async |
| Module navigation | ≤ 150ms | 300ms | Compiled x:Bind; lightweight VMs |
| Inventory grid scroll (5,000+) | 60fps | No stutter | ItemsRepeater + flat templates |

### 14.2 Hot Path Rules (Non-Negotiable)

- **Dapper / raw ADO.NET** for all hot paths — EF Core is banned from hot paths
- **SQLite FTS5** for product search — LIKE queries are banned
- **Async everything** — no synchronous I/O on UI thread, ever
- **Query must have index** before it ships — verify with EXPLAIN QUERY PLAN
- **No N+1 queries** in list views — single round-trip per list
- **No cloud call** in the search/barcode/cart path — local only

### 14.3 UI Performance Rules

- No Acrylic/Mica/blur on data-dense screens (POS, inventory grids)
- No animations on list insert/remove in data views
- `ItemsRepeater` with virtualization for every list > 50 rows
- Flat row templates — no nested Grid inside nested Border inside nested StackPanel
- `x:Bind` (compiled bindings) on all hot screens
- No images in default search results row (lazy-load only on hover/detail)

### 14.4 Startup Optimization

- **Lazy-load:** Accounting engine, B2B module, Reports engine, AI layer — all initialize on first navigation
- **Defer:** Product master sync check, license check, telemetry — after POS is interactive
- **ReadyToRun compilation** for the published binary to cut JIT warm-up

### 14.5 Debug Performance Telemetry

Add in debug builds (low cost, high value):
```csharp
// Wrap every TRD-tracked operation
using var t = PerfTimer.Start("BarcodeScan");
// ... operation ...
t.StopAndLog(ceiling: 200); // Logs warning if > 200ms
```

---

## 15. UI/UX Architecture

### 15.1 App Shell

```
┌────────────────────────────────────────────────────────────┐
│ [🔍 Search anything...] [Branch▼] [Sync●] [👤 User] [🔔] │
├───────────────┬────────────────────────────────────────────┤
│               │                                            │
│  Dashboard    │                                            │
│  POS          │              WORKSPACE                     │
│  Sales        │                                            │
│  Purchase     │                                            │
│  Inventory    │                                            │
│  Customers    │                                            │
│  Suppliers    │                                            │
│  Accounting   │                                            │
│  B2B Network  │                                            │
│  Reports      │                                            │
│  Settings     │                                            │
│               │                                            │
└───────────────┴────────────────────────────────────────────┘
```

**POS gets its own dedicated full-screen workspace** (not a generic CRUD screen).

### 15.2 Command Palette (Ctrl+K)

Global action search — power users activate actions without navigating menus:
```
> Create sale           [F2]
> Product search        [F3]
> Open customer: Rahul
> Stock adjustment
> INV-10291             → opens invoice
> BATCH-98281           → opens batch detail
> GST report           → last 30 days
```

### 15.3 Role-Based Dashboards

| Role | Key Widgets |
|---|---|
| Owner | Revenue today, Gross Profit %, Outstanding, Stock value, Expiry risk ₹, Top 5 products |
| Manager | Sales by counter, Cashier performance, Low stock alerts, Pending B2B orders |
| Pharmacist | Prescriptions pending, Schedule-H alerts, Near-expiry batches, Stock status |
| Accountant | Receivables aging, Payables aging, GST due, Pending bank reconciliation |

### 15.4 UX Principles

1. **Copy operational efficiency from MARG; replace the visual design** with clean modern WinUI 3 UI
2. **Barcode-first + keyboard-first** — mouse is secondary
3. **Explainability:** Show calculation breakdown (MRP ₹150 → Discount 10% → GST 12% → Final ₹118.80)
4. **Transaction timeline** visible on every B2B and major transaction
5. **Guided onboarding** for new staff (step-by-step wizard first time)
6. **Global search** finds: invoices, customers, products, batches, suppliers, POs by one query

---

## 16. Hardware Abstraction Layer

Define interfaces; never hardcode device models:

```csharp
public interface IBarcodeScanner { event EventHandler<string> BarcodeScanned; }
public interface IReceiptPrinter  { Task PrintAsync(ReceiptDocument doc); }
public interface ICashDrawer      { Task OpenAsync(); }
public interface IBarcodeLabel    { Task PrintAsync(BarcodeLabelDocument doc); }
public interface IWeighingScale   { Task<decimal> ReadWeightAsync(); }
public interface ICustomerDisplay { Task ShowAsync(CartSummary summary); }
```

Supported hardware categories:
- Barcode Scanners (USB HID keyboard mode — no driver needed)
- Thermal Receipt Printers (ESC/POS — Star, Epson, etc.)
- Dot-Matrix Printers (for older pharmacies)
- A4 Laser Printers (GST invoice, purchase orders)
- Cash Drawers (RJ11 kick pulse via printer)
- Barcode Label Printers (for generating product labels)

---

## 17. Integration Layer

Each integration is replaceable and must never block the core transaction:

| Integration | Protocol | Mode |
|---|---|---|
| GST E-Invoice API (NIC/IRP) | REST/JSON | Async via outbox |
| E-Way Bill API | REST/JSON | Async via outbox |
| Payment Gateway (Razorpay/PhonePe) | Webhook | Async |
| SMS (Twilio / MSG91) | REST | Async notification queue |
| WhatsApp Business API | REST | Async notification queue |
| Email (SMTP / SendGrid) | SMTP/REST | Async queue |
| ABHA (National Health ID) | REST | Optional, async |
| Banking / Reconciliation | CSV import / API | Background sync |
| Legacy ERP Import (MARG, Tally) | CSV/Excel mapper | One-time migration tool |

---

## 18. Observability & Monitoring

### 18.1 Metrics (OpenTelemetry → Grafana / Azure Monitor)

- API p50/p95/p99 latency per endpoint
- DB query duration (flag any > 100ms)
- Outbox queue size and drain rate
- Sync failure rate per device
- Stock conflict rate
- Error rate by module
- CPU / Memory / Disk on local server

### 18.2 Structured Logs (Serilog)

Every log line includes: `org_id`, `branch_id`, `user_id`, `device_id`, `trace_id`

Log categories: Authentication, Transactions, Sync, Errors, Security, Audit

### 18.3 Alerts

- Outbox queue > 500 events unsynced → alert
- Any operation exceeding hard ceiling latency → alert
- Failed login > 5 attempts → security alert
- Stock goes negative → critical alert

---

## 19. Deployment Architecture

### 19.1 Environments

```
LOCAL → DEV → TEST → STAGING → PRODUCTION
```

Never experiment against PRODUCTION.

### 19.2 Local Server Deployment (Per Pharmacy)

- Packaged as Windows Service (ASP.NET Core)
- PostgreSQL installed alongside (bundled or separate)
- Auto-start on machine boot
- Local admin UI for health check, outbox status, sync status
- Offline installer package (no internet required to install)

### 19.3 Cloud Deployment (Docker + Terraform)

```
Cloud infra:
  API → Docker container (auto-scaling)
  PostgreSQL → Managed (Azure DB for PostgreSQL / AWS RDS)
  Redis → Managed (Azure Cache for Redis)
  Storage → Azure Blob / S3
  WAF → Cloudflare / Azure Front Door
```

### 19.4 Update System (Desktop)

```
Cloud Version Server
    ↓
Desktop checks on startup (background, non-blocking)
    ↓
Download signed MSIX package
    ↓
Verify code signature + hash
    ↓
Backup local SQLite DB
    ↓
Run database migrations
    ↓
Apply update
    ↓
Health check (startup validation)
    ↓
Alert if migration failed → rollback
```

### 19.5 Backup Strategy

```
Local PostgreSQL:
  - WAL archiving (continuous)
  - Daily pg_dump → encrypted, off-site
  - Local SQLite → daily backup to local server

Cloud PostgreSQL:
  - Continuous WAL (PITR — point-in-time recovery)
  - Daily snapshot
  - Weekly off-site to separate region
  - Test restoration quarterly
```

**RPO target:** 5 minutes | **RTO target:** 30 minutes

---

## 20. Development Phases (Roadmap)

| Phase | What to Build | Gate Criteria |
|---|---|---|
| **Phase 0** | Architecture docs, domain model, ERD, API contracts, security model, offline strategy, UX architecture. No feature code. | All 12 foundation documents complete |
| **Phase 1** | Organization, Users, RBAC, Products, Suppliers, Customers, Warehouse, Batch, Inventory, Stock Ledger, Purchase, Sale, Returns | Barcode-scan-to-cart vertical slice passing TRD latency targets on baseline rig |
| **Phase 2** | Full POS: barcode, FTS5 search, cart, discount/scheme engine, payment, invoice, print, hold/resume bill, sale returns | POS latency targets met. Multi-cashier concurrency test passing |
| **Phase 3** | Accounting engine: Chart of Accounts, Journal, auto-posting, Receivable, Payable, P&L, Balance Sheet, Trial Balance | Double-entry verified correct across 1,000 transaction test suite |
| **Phase 4** | GST compliance: CGST/SGST/IGST, E-Invoice, E-Way Bill, GSTR-1/2/3B, TDS, document retention | GST calculations audited against official rules |
| **Phase 5** | Offline/LAN: Local PostgreSQL server, local API, SQLite outbox, sync engine, conflict resolution, recovery testing | Survive: internet down / server down / PC crash / power failure / duplicate request |
| **Phase 6** | Multi-branch: branch management, inter-branch stock transfers, consolidated reporting, branch isolation | Stock transfer state machine fully tested; no cross-branch data leak |
| **Phase 7** | B2B Commerce Network: purchase request, quotation, SO/PO, pick/pack, delivery/pickup, invoice, receipt, discrepancy, returns | Full B2B transaction lifecycle end-to-end tested between two tenant ERPs |
| **Phase 8** | Cloud management: multi-tenant cloud dashboard, remote reports, remote monitoring, B2B marketplace | Multi-tenant isolation verified (pen test); cloud backup tested |
| **Phase 9** | AI layer: demand forecasting, smart reorder, expiry prediction, anomaly detection, NL reporting, invoice OCR | AI is advisory only; cannot execute financial transactions autonomously |

---

## 21. Testing Strategy

### 21.1 Test Types Required

| Type | Focus |
|---|---|
| **Unit** | Domain business rules (pricing, GST calc, stock deduction logic) |
| **Integration** | Database transactions, outbox processing, API endpoints |
| **Contract** | API compatibility across versions |
| **E2E** | Full business flows (full sale → payment → accounting → sync) |
| **Concurrency** | Multiple cashiers selling same product simultaneously |
| **Offline** | Network failure → local commit → reconnect → sync |
| **Security** | SQL injection, broken access control, privilege escalation, tenant isolation |
| **Recovery** | Kill process mid-transaction → restart → verify DB in valid state |
| **Performance** | Latency targets on baseline rig |

### 21.2 Critical Concurrency Test

```
Simulate: Cashier A + Cashier B + Cashier C selling same product simultaneously
Verify: 
  - Stock never goes negative (atomic UPDATE with WHERE quantity >= @qty)
  - Invoice numbers are unique (no INV-1002 duplicated)
  - All accounting entries are correct (Debit = Credit)
  - Audit log has all three transactions
```

### 21.3 Critical Recovery Test

Deliberately kill process during:
- Sale creation (between items insert and stock deduct)
- Payment recording
- Outbox event write
- DB commit

Restart → verify system is in valid, consistent state.

---

## 22. What NOT to Build

| Approach | Why Not |
|---|---|
| ❌ Flutter desktop as primary Windows ERP | Poor fit for native Windows, hardware integration complexity |
| ❌ Electron | Runtime overhead unacceptable for performance-sensitive ERP |
| ❌ Microservices from day one | Unnecessary operational complexity before you have load |
| ❌ Firebase as core ERP database | Poor fit for relational accounting / inventory domain |
| ❌ Shared SQLite file over network (SMB) | Catastrophic for concurrent pharmacy transactions |
| ❌ Cloud-only POS | Pharmacy must survive internet outage |
| ❌ Hardcoded GST/tax rates | Regulations change; rates must be versioned |
| ❌ AI controlling accounting or stock | AI is advisory layer only; deterministic ERP rules execute |
| ❌ DELETE on financial records | Use reversal/credit note/correction mechanisms |
| ❌ EF Core on hot path (search, POS) | Query translation + change-tracking overhead is too expensive |
| ❌ LIKE '%query%' for product search | Full-table scan on 50k+ SKUs; use FTS5 |
| ❌ Synchronous cloud call in sale path | GST API down = pharmacy can't sell |
| ❌ MAX(invoice_no)+1 for document numbers | Race condition; use sequence service |
| ❌ 1,000 separate screens | Deep workflows with reusable components |
| ❌ Redis as financial database | Redis is cache only; authoritative state stays in PostgreSQL |
| ❌ Shortcuts wired directly to business logic | Must use Command Layer — shortcut is a config binding only |
| ❌ Global-only shortcuts without scope system | Same key (F2) means different things per screen — scope is mandatory |
| ❌ Ctrl+S = Flush Cache in Standard profile | Financial-safety risk; Ctrl+S must = Save in Medistock Standard profile |

---

## 16. Keyboard & Input Architecture

> **Full specification:** [`KEYBOARD_SHORTCUTS.md`](./KEYBOARD_SHORTCUTS.md)  
> This section summarizes the architecture principles only.

### Core Principle: Command Layer First

Every user action — keyboard, mouse, barcode, touch — invokes an `ICommand`. Shortcuts are a **config/binding layer** on top of commands, never wired directly to business logic.

```
Keyboard / Mouse / Barcode / Touch
    ↓
ICommand (e.g. SaveSaleCommand, ApplyDiscountCommand)
    ↓
Application Service / Domain Logic
    ↓
Database / Side effects
```

### Two Keymap Profiles

| Profile | Ctrl+S | Save Key | Best For |
|---|---|---|---|
| **Medistock Standard** (default) | Save | Ctrl+S / Ctrl+W | New users, non-MARG staff |
| **MARG-Compatible** | Flush Cache | Ctrl+W | Migrating MARG users |

Profile is **user-selectable** in Settings → Keyboard. Applied instantly, no restart. Stored in `AppLocalSettings`.

### Scope System

Keys are context-scoped per active view. Deepest scope wins:

```
Global scope → Screen scope → Panel scope → Field scope
```

Example: F2 = New Sale (POS screen scope) but F2 = View Tax Status (qty field scope, inside POS). No conflict — deepest scope wins.

### Critical Shortcuts (Every developer must know these)

| Shortcut | Standard | MARG-Compat | Screen |
|---|---|---|---|
| `F2` | New Sale | New Sale (Alt+N) | POS |
| `F3` | Product Search | Product Search | POS |
| `F4` | Apply Discount | Apply Discount | POS |
| `F5` | Hold Bill | Hold Bill | POS |
| `F6` | Open Payment | Open Payment | POS |
| `Ctrl+W` | Save Bill | Save Bill | POS |
| `Ctrl+S` | Save Bill | **Flush Cache** | Global |
| `F12` | Calculator | Calculator | Global |
| `Ctrl+K` | Command Palette | Command Palette | Global |
| `Alt+F1` | Shortcut Help Overlay | Shortcut Help | Global |
| `Alt+V` | Voucher Entry | Voucher Entry | Accounting |
| `F2–F7` | Switch voucher types | Switch voucher types | Accounting |

### Barcode Scanner Protocol

Barcode scanners operate as HID keyboard devices (USB). The POS search field must be focus-ready at all times. After every cart action completes, focus is programmatically returned to the search box — zero extra keystrokes between consecutive scans.

### Mouse Parity

Every keyboard shortcut action is **also accessible via mouse**. All shortcut-capable buttons show their shortcut in the tooltip: `"Complete payment and close bill (F6)"`.

### In-App Help

`Alt+F1` from any screen opens a context-aware shortcut overlay showing only shortcuts active in the current scope. Never all 100+ shortcuts — only what's relevant right now.

---

*This document is the single source of architectural truth for Medistock. All implementation decisions must be consistent with it.*  
*Related documents: [`UI_DESIGN_SYSTEM.md`](./UI_DESIGN_SYSTEM.md) · [`KEYBOARD_SHORTCUTS.md`](./KEYBOARD_SHORTCUTS.md) · [`trd.md`](./trd.md) · [`AGY_STATE.md`](./AGY_STATE.md)*

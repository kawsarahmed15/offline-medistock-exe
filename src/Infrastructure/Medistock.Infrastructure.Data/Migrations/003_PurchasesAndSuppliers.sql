-- Migration: 003_PurchasesAndSuppliers.sql
-- Description: Creates suppliers, purchase_invoices, and purchase_invoice_items tables

CREATE TABLE IF NOT EXISTS suppliers (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    name TEXT NOT NULL,
    gstin TEXT,
    dl_number TEXT,
    phone TEXT,
    email TEXT,
    address TEXT,
    credit_days INTEGER NOT NULL DEFAULT 30,
    outstanding_balance REAL NOT NULL DEFAULT 0.0,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_suppliers_org_name ON suppliers(org_id, name);
CREATE INDEX IF NOT EXISTS idx_suppliers_gstin ON suppliers(gstin);

CREATE TABLE IF NOT EXISTS purchase_invoices (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    warehouse_id TEXT NOT NULL,
    supplier_id TEXT NOT NULL,
    supplier_name TEXT NOT NULL,
    supplier_gstin TEXT,
    supplier_invoice_no TEXT NOT NULL,
    supplier_invoice_date TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 0,
    is_interstate INTEGER NOT NULL DEFAULT 0,
    subtotal REAL NOT NULL DEFAULT 0,
    discount_amount REAL NOT NULL DEFAULT 0,
    taxable_amount REAL NOT NULL DEFAULT 0,
    cgst_amount REAL NOT NULL DEFAULT 0,
    sgst_amount REAL NOT NULL DEFAULT 0,
    igst_amount REAL NOT NULL DEFAULT 0,
    round_off REAL NOT NULL DEFAULT 0,
    grand_total REAL NOT NULL DEFAULT 0,
    notes TEXT,
    created_by_user_id TEXT NOT NULL,
    created_at TEXT NOT NULL,
    posted_at TEXT,
    FOREIGN KEY(supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS idx_purchases_supplier ON purchase_invoices(supplier_id);
CREATE INDEX IF NOT EXISTS idx_purchases_date ON purchase_invoices(supplier_invoice_date);
CREATE INDEX IF NOT EXISTS idx_purchases_inv_no ON purchase_invoices(supplier_invoice_no);

CREATE TABLE IF NOT EXISTS purchase_invoice_items (
    id TEXT PRIMARY KEY,
    purchase_invoice_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    product_name TEXT NOT NULL,
    hsn_code TEXT NOT NULL,
    batch_number TEXT NOT NULL,
    expiry_date TEXT NOT NULL,
    manufacturing_date TEXT,
    quantity REAL NOT NULL,
    free_quantity REAL NOT NULL DEFAULT 0,
    unit_price REAL NOT NULL,
    mrp REAL NOT NULL,
    sale_rate REAL NOT NULL,
    discount_pct REAL NOT NULL DEFAULT 0,
    discount_amount REAL NOT NULL DEFAULT 0,
    taxable_amount REAL NOT NULL,
    gst_rate_percent REAL NOT NULL,
    cgst_rate REAL NOT NULL DEFAULT 0,
    cgst_amount REAL NOT NULL DEFAULT 0,
    sgst_rate REAL NOT NULL DEFAULT 0,
    sgst_amount REAL NOT NULL DEFAULT 0,
    igst_rate REAL NOT NULL DEFAULT 0,
    igst_amount REAL NOT NULL DEFAULT 0,
    net_amount REAL NOT NULL,
    FOREIGN KEY(purchase_invoice_id) REFERENCES purchase_invoices(id) ON DELETE CASCADE,
    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS idx_purchase_items_inv ON purchase_invoice_items(purchase_invoice_id);
CREATE INDEX IF NOT EXISTS idx_purchase_items_prod ON purchase_invoice_items(product_id);

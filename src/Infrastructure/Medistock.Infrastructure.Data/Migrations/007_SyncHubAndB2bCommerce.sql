-- Migration 007: Server Sync Hub and B2B Wholesaler Commerce Engine

-- Table: server_sync_events (Monotonic sequence event log for Cloud & Local Server)
CREATE TABLE IF NOT EXISTS server_sync_events (
    server_sequence_number INTEGER PRIMARY KEY AUTOINCREMENT,
    event_id TEXT NOT NULL,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    device_id TEXT NOT NULL,
    event_type TEXT NOT NULL,
    aggregate_id TEXT NOT NULL,
    idempotency_key TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    client_created_at TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'utc'))
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_server_sync_idempotency 
ON server_sync_events(org_id, idempotency_key);

CREATE INDEX IF NOT EXISTS idx_server_sync_pull 
ON server_sync_events(org_id, branch_id, server_sequence_number);

-- B2B Wholesaler Marketplace & Digital Catalog
CREATE TABLE IF NOT EXISTS b2b_wholesalers (
    wholesaler_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    gstin TEXT NOT NULL,
    drug_license_no TEXT NOT NULL,
    phone TEXT NOT NULL,
    email TEXT NOT NULL,
    city TEXT NOT NULL,
    state TEXT NOT NULL,
    min_order_value REAL NOT NULL DEFAULT 0,
    credit_days INTEGER NOT NULL DEFAULT 30,
    is_verified INTEGER NOT NULL DEFAULT 1,
    rating REAL NOT NULL DEFAULT 4.8,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'utc'))
);

CREATE TABLE IF NOT EXISTS b2b_wholesaler_catalogs (
    catalog_id TEXT PRIMARY KEY,
    wholesaler_id TEXT NOT NULL,
    product_code TEXT NOT NULL,
    brand_name TEXT NOT NULL,
    generic_name TEXT NOT NULL,
    dosage_form TEXT NOT NULL,
    strength TEXT NOT NULL,
    manufacturer TEXT NOT NULL,
    hsn_code TEXT NOT NULL,
    mrp REAL NOT NULL,
    wholesale_rate REAL NOT NULL,
    gst_rate REAL NOT NULL,
    available_stock INTEGER NOT NULL DEFAULT 100,
    scheme_description TEXT,
    min_order_qty INTEGER NOT NULL DEFAULT 1,
    free_ratio_buy INTEGER NOT NULL DEFAULT 0,
    free_ratio_get INTEGER NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'utc')),
    FOREIGN KEY(wholesaler_id) REFERENCES b2b_wholesalers(wholesaler_id)
);

CREATE INDEX IF NOT EXISTS idx_b2b_catalog_search 
ON b2b_wholesaler_catalogs(brand_name, generic_name);

-- B2B Purchase Orders
CREATE TABLE IF NOT EXISTS b2b_purchase_orders (
    order_id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    order_number TEXT NOT NULL UNIQUE,
    wholesaler_id TEXT NOT NULL,
    status TEXT NOT NULL, -- Draft, Submitted, Confirmed, Dispatched, Delivered, Cancelled
    sub_total REAL NOT NULL,
    tax_amount REAL NOT NULL,
    total_amount REAL NOT NULL,
    delivery_address TEXT NOT NULL,
    notes TEXT,
    dispatch_tracking_no TEXT,
    ordered_at TEXT NOT NULL,
    expected_delivery TEXT,
    delivered_at TEXT,
    converted_purchase_invoice_id TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'utc')),
    FOREIGN KEY(wholesaler_id) REFERENCES b2b_wholesalers(wholesaler_id)
);

CREATE TABLE IF NOT EXISTS b2b_purchase_order_items (
    order_item_id TEXT PRIMARY KEY,
    order_id TEXT NOT NULL,
    catalog_id TEXT NOT NULL,
    product_code TEXT NOT NULL,
    product_name TEXT NOT NULL,
    order_qty INTEGER NOT NULL,
    free_qty INTEGER NOT NULL DEFAULT 0,
    unit_wholesale_rate REAL NOT NULL,
    gst_rate REAL NOT NULL,
    tax_amount REAL NOT NULL,
    total_amount REAL NOT NULL,
    FOREIGN KEY(order_id) REFERENCES b2b_purchase_orders(order_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_b2b_orders_org 
ON b2b_purchase_orders(org_id, branch_id, status);

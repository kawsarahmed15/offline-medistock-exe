-- Migration 005: Sales Returns, Credit Notes & Batch Restocking

CREATE TABLE IF NOT EXISTS sale_returns (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    counter_id TEXT NOT NULL,
    warehouse_id TEXT NOT NULL,
    original_sale_id TEXT NOT NULL REFERENCES sales(id),
    original_invoice_no TEXT NOT NULL,
    credit_note_no TEXT NOT NULL UNIQUE,
    return_date TEXT NOT NULL,
    customer_id TEXT,
    customer_name TEXT,
    user_id TEXT NOT NULL,
    device_id TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 0: Draft, 1: Posted, 2: Cancelled
    reason TEXT,
    subtotal REAL NOT NULL DEFAULT 0.0,
    tax_amount REAL NOT NULL DEFAULT 0.0,
    round_off REAL NOT NULL DEFAULT 0.0,
    total_amount REAL NOT NULL DEFAULT 0.0,
    refund_mode INTEGER NOT NULL DEFAULT 1, -- 1: Cash, 2: Card, 3: Upi, 4: Credit
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    posted_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_sale_returns_sale ON sale_returns(original_sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_returns_cn ON sale_returns(credit_note_no);
CREATE INDEX IF NOT EXISTS idx_sale_returns_date ON sale_returns(org_id, branch_id, return_date);

CREATE TABLE IF NOT EXISTS sale_return_items (
    id TEXT PRIMARY KEY,
    sale_return_id TEXT NOT NULL REFERENCES sale_returns(id) ON DELETE CASCADE,
    sale_item_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    product_name TEXT NOT NULL,
    batch_id TEXT NOT NULL,
    batch_number TEXT NOT NULL,
    quantity REAL NOT NULL,
    unit_price REAL NOT NULL,
    taxable_amount REAL NOT NULL,
    cgst_rate REAL NOT NULL DEFAULT 0.0,
    cgst_amount REAL NOT NULL DEFAULT 0.0,
    sgst_rate REAL NOT NULL DEFAULT 0.0,
    sgst_amount REAL NOT NULL DEFAULT 0.0,
    igst_rate REAL NOT NULL DEFAULT 0.0,
    igst_amount REAL NOT NULL DEFAULT 0.0,
    net_amount REAL NOT NULL,
    restock_decision INTEGER NOT NULL DEFAULT 1, -- 1: RestockToAvailable, 2: QuarantineDamaged, 3: QuarantineExpired
    reason TEXT
);

CREATE INDEX IF NOT EXISTS idx_sale_return_items_return ON sale_return_items(sale_return_id);
CREATE INDEX IF NOT EXISTS idx_sale_return_items_batch ON sale_return_items(batch_id);

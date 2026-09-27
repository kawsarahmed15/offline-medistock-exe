-- Migration 006: Inter-Branch Stock Transfers & Multi-Branch Reconciliation

CREATE TABLE IF NOT EXISTS stock_transfers (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    transfer_no TEXT NOT NULL UNIQUE,
    source_branch_id TEXT NOT NULL,
    source_warehouse_id TEXT NOT NULL,
    destination_branch_id TEXT NOT NULL,
    destination_warehouse_id TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 0: Draft, 1: Requested, 2: InTransit, 3: ReceivedCompleted, 4: Cancelled, 5: Disputed
    notes TEXT,
    requested_by_user_id TEXT NOT NULL,
    dispatched_by_user_id TEXT,
    received_by_user_id TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    dispatched_at TEXT,
    received_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_stock_transfers_org ON stock_transfers(org_id, status);
CREATE INDEX IF NOT EXISTS idx_stock_transfers_no ON stock_transfers(transfer_no);
CREATE INDEX IF NOT EXISTS idx_stock_transfers_src ON stock_transfers(source_branch_id);
CREATE INDEX IF NOT EXISTS idx_stock_transfers_dest ON stock_transfers(destination_branch_id);

CREATE TABLE IF NOT EXISTS stock_transfer_items (
    id TEXT PRIMARY KEY,
    transfer_id TEXT NOT NULL REFERENCES stock_transfers(id) ON DELETE CASCADE,
    product_id TEXT NOT NULL,
    product_name TEXT NOT NULL,
    batch_id TEXT NOT NULL,
    batch_number TEXT NOT NULL,
    expiry_date TEXT NOT NULL,
    requested_quantity REAL NOT NULL,
    dispatched_quantity REAL NOT NULL,
    received_quantity REAL NOT NULL DEFAULT 0.0,
    discrepancy_quantity REAL NOT NULL DEFAULT 0.0,
    unit_cost REAL NOT NULL DEFAULT 0.0
);

CREATE INDEX IF NOT EXISTS idx_transfer_items_tr ON stock_transfer_items(transfer_id);
CREATE INDEX IF NOT EXISTS idx_transfer_items_batch ON stock_transfer_items(batch_id);

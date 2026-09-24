-- Migration 004: Double-Entry Financial Accounting & Ledgers

CREATE TABLE IF NOT EXISTS account_heads (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    code TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    category INTEGER NOT NULL, -- 1: Asset, 2: Liability, 3: Equity, 4: Revenue, 5: Expense
    parent_account_id TEXT REFERENCES account_heads(id),
    is_system_account INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    current_balance REAL NOT NULL DEFAULT 0.0,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_account_heads_org ON account_heads(org_id, category);
CREATE INDEX IF NOT EXISTS idx_account_heads_code ON account_heads(code);

CREATE TABLE IF NOT EXISTS journal_entries (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    voucher_number TEXT NOT NULL UNIQUE,
    voucher_type INTEGER NOT NULL, -- 1: Sales, 2: Purchase, 3: Payment, 4: Receipt, 5: Contra, 6: Journal, 7: CreditNote, 8: DebitNote
    voucher_date TEXT NOT NULL,
    narration TEXT,
    reference_id TEXT,
    reference_type TEXT,
    created_by_user_id TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_journal_entries_date ON journal_entries(org_id, branch_id, voucher_date);
CREATE INDEX IF NOT EXISTS idx_journal_entries_voucher ON journal_entries(voucher_number);
CREATE INDEX IF NOT EXISTS idx_journal_entries_ref ON journal_entries(reference_id);

CREATE TABLE IF NOT EXISTS journal_lines (
    id TEXT PRIMARY KEY,
    journal_entry_id TEXT NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id TEXT NOT NULL REFERENCES account_heads(id),
    account_name TEXT NOT NULL,
    debit_amount REAL NOT NULL DEFAULT 0.0,
    credit_amount REAL NOT NULL DEFAULT 0.0,
    narration TEXT
);

CREATE INDEX IF NOT EXISTS idx_journal_lines_account ON journal_lines(account_id, journal_entry_id);

-- Standard Pharmacy Chart of Accounts Initial Seed
INSERT OR IGNORE INTO account_heads (id, org_id, code, name, category, parent_account_id, is_system_account, is_active, current_balance, created_at) VALUES
('acc_cash', 'org-1', '1001', 'Cash on Hand', 1, NULL, 1, 1, 0.0, datetime('now')),
('acc_bank_hdfc', 'org-1', '1002', 'Bank Account (Primary)', 1, NULL, 1, 1, 0.0, datetime('now')),
('acc_debtors', 'org-1', '1003', 'Sundry Debtors (Customers)', 1, NULL, 1, 1, 0.0, datetime('now')),
('acc_stock', 'org-1', '1004', 'Pharmacy Stock Asset', 1, NULL, 1, 1, 0.0, datetime('now')),
('acc_input_cgst', 'org-1', '1005', 'Input CGST A/c', 1, NULL, 1, 1, 0.0, datetime('now')),
('acc_input_sgst', 'org-1', '1006', 'Input SGST A/c', 1, NULL, 1, 1, 0.0, datetime('now')),
('acc_input_igst', 'org-1', '1007', 'Input IGST A/c', 1, NULL, 1, 1, 0.0, datetime('now')),

('acc_creditors', 'org-1', '2001', 'Sundry Creditors (Suppliers)', 2, NULL, 1, 1, 0.0, datetime('now')),
('acc_output_cgst', 'org-1', '2002', 'Output CGST A/c', 2, NULL, 1, 1, 0.0, datetime('now')),
('acc_output_sgst', 'org-1', '2003', 'Output SGST A/c', 2, NULL, 1, 1, 0.0, datetime('now')),
('acc_output_igst', 'org-1', '2004', 'Output IGST A/c', 2, NULL, 1, 1, 0.0, datetime('now')),

('acc_capital', 'org-1', '3001', 'Owner / Partner Capital', 3, NULL, 1, 1, 0.0, datetime('now')),
('acc_retained', 'org-1', '3002', 'Retained Earnings', 3, NULL, 1, 1, 0.0, datetime('now')),

('acc_sales', 'org-1', '4001', 'Pharmacy Medicine Sales A/c', 4, NULL, 1, 1, 0.0, datetime('now')),
('acc_disc_rec', 'org-1', '4002', 'Discount Received A/c', 4, NULL, 1, 1, 0.0, datetime('now')),
('acc_other_inc', 'org-1', '4003', 'Other Operating Income', 4, NULL, 1, 1, 0.0, datetime('now')),

('acc_purchases', 'org-1', '5001', 'Pharmacy Medicine Purchases A/c', 5, NULL, 1, 1, 0.0, datetime('now')),
('acc_disc_all', 'org-1', '5002', 'Discount Allowed A/c', 5, NULL, 1, 1, 0.0, datetime('now')),
('acc_roundoff', 'org-1', '5003', 'Round Off Expense / Income', 5, NULL, 1, 1, 0.0, datetime('now')),
('acc_stock_loss', 'org-1', '5004', 'Stock Damage / Expiry Loss A/c', 5, NULL, 1, 1, 0.0, datetime('now')),
('acc_rent_exp', 'org-1', '5005', 'Store Rent Expense', 5, NULL, 0, 1, 0.0, datetime('now')),
('acc_salaries', 'org-1', '5006', 'Staff Salaries Expense', 5, NULL, 0, 1, 0.0, datetime('now')),
('acc_electricity', 'org-1', '5007', 'Electricity & Utilities', 5, NULL, 0, 1, 0.0, datetime('now'));

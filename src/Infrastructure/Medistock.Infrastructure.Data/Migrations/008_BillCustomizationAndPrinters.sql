CREATE TABLE IF NOT EXISTS bill_templates (
    id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    is_default INTEGER NOT NULL DEFAULT 0,
    paper_size TEXT NOT NULL,
    config_json TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_bill_templates_default ON bill_templates(is_default);

CREATE TABLE IF NOT EXISTS printer_configurations (
    id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    interface_type TEXT NOT NULL,
    target_name_or_ip TEXT NOT NULL,
    target_port INTEGER DEFAULT 9100,
    paper_size TEXT NOT NULL,
    assigned_template_id TEXT,
    auto_cut_paper INTEGER NOT NULL DEFAULT 1,
    kick_cash_drawer INTEGER NOT NULL DEFAULT 1,
    is_default_pos INTEGER NOT NULL DEFAULT 0,
    is_default_a4 INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY(assigned_template_id) REFERENCES bill_templates(id)
);

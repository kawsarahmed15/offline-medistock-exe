-- Migration: 002_InventoryAndScheduleDrugs.sql
-- Description: Creates schedule_drug_register table for statutory Drug Schedules (H, H1, X, Narcotics)

CREATE TABLE IF NOT EXISTS schedule_drug_register (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    sale_id TEXT NOT NULL,
    invoice_no TEXT NOT NULL,
    sale_date TEXT NOT NULL,
    product_id TEXT NOT NULL,
    product_name TEXT NOT NULL,
    schedule INTEGER NOT NULL,
    batch_number TEXT NOT NULL,
    expiry_date TEXT NOT NULL,
    quantity REAL NOT NULL,
    patient_name TEXT NOT NULL,
    patient_address TEXT,
    patient_phone TEXT,
    doctor_name TEXT NOT NULL,
    doctor_reg_no TEXT,
    doctor_address TEXT,
    prescription_ref TEXT,
    prescription_date TEXT,
    dispensed_by_user_id TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_sch_reg_sale_date ON schedule_drug_register(sale_date);
CREATE INDEX IF NOT EXISTS idx_sch_reg_schedule ON schedule_drug_register(schedule);
CREATE INDEX IF NOT EXISTS idx_sch_reg_product ON schedule_drug_register(product_id);
CREATE INDEX IF NOT EXISTS idx_sch_reg_patient_phone ON schedule_drug_register(patient_phone);

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;

namespace Medistock.Infrastructure.Data.Migrations;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public class DatabaseMigrator : IDatabaseMigrator
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public DatabaseMigrator(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        // Ensure migrations tracking table exists
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS __schema_migrations (
                version TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
        ");

        var migrations = new[]
        {
            ("001_InitialSchema", GetInitialSchemaSql()),
            ("002_InventoryAndScheduleDrugs", GetScheduleDrugsSchemaSql()),
            ("003_PurchasesAndSuppliers", GetPurchasesSchemaSql())
        };

        foreach (var (version, sql) in migrations)
        {
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM __schema_migrations WHERE version = @version",
                new { version });

            if (exists == 0)
            {
                using var transaction = connection.BeginTransaction();
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = (System.Data.Common.DbTransaction)transaction;
                    cmd.CommandText = sql;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);

                    await connection.ExecuteAsync(
                        "INSERT INTO __schema_migrations (version, applied_at) VALUES (@version, @appliedAt)",
                        new { version, appliedAt = DateTime.UtcNow.ToString("o") },
                        transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    private static string GetInitialSchemaSql()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resourceName = "Medistock.Infrastructure.Data.Migrations.001_InitialSchema.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Fallback to local file lookup if embedded resource is not packaged yet
        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "001_InitialSchema.sql");
        if (File.Exists(localPath))
        {
            return File.ReadAllText(localPath);
        }

        // Return direct SQL string definition if stream/file not found
        return DirectSqlSchema;
    }

    private const string DirectSqlSchema = @"
CREATE TABLE IF NOT EXISTS products (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    name TEXT NOT NULL,
    brand_name TEXT NOT NULL,
    generic_name TEXT NOT NULL,
    composition TEXT NOT NULL DEFAULT '',
    strength TEXT NOT NULL DEFAULT '',
    dosage_form INTEGER NOT NULL DEFAULT 0,
    pack_units INTEGER NOT NULL DEFAULT 10,
    base_unit TEXT NOT NULL DEFAULT 'TAB',
    hsn_code TEXT NOT NULL DEFAULT '3004',
    gst_rate_percent REAL NOT NULL DEFAULT 12.0,
    schedule INTEGER NOT NULL DEFAULT 0,
    is_prescription_required INTEGER NOT NULL DEFAULT 0,
    is_cold_chain INTEGER NOT NULL DEFAULT 0,
    is_narcotic INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    primary_barcode TEXT,
    manufacturer_id TEXT,
    manufacturer_name TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_products_org ON products(org_id);
CREATE INDEX IF NOT EXISTS idx_products_hsn ON products(hsn_code);
CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(primary_barcode);

CREATE TABLE IF NOT EXISTS product_barcodes (
    id TEXT PRIMARY KEY,
    product_id TEXT NOT NULL,
    barcode TEXT NOT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_product_barcodes_val ON product_barcodes(barcode);
CREATE INDEX IF NOT EXISTS idx_product_barcodes_prod ON product_barcodes(product_id);

CREATE TABLE IF NOT EXISTS batches (
    id TEXT PRIMARY KEY,
    product_id TEXT NOT NULL,
    org_id TEXT NOT NULL,
    batch_number TEXT NOT NULL,
    expiry_date TEXT NOT NULL,
    manufacturing_date TEXT,
    mrp REAL NOT NULL,
    purchase_rate REAL NOT NULL,
    sale_rate REAL NOT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS idx_batches_product_expiry ON batches(product_id, expiry_date);
CREATE INDEX IF NOT EXISTS idx_batches_org_batch ON batches(org_id, batch_number);

CREATE TABLE IF NOT EXISTS stock_balances (
    id TEXT PRIMARY KEY,
    batch_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    warehouse_id TEXT NOT NULL,
    location_id TEXT,
    quantity REAL NOT NULL DEFAULT 0,
    reserved_quantity REAL NOT NULL DEFAULT 0,
    last_updated_at TEXT NOT NULL,
    FOREIGN KEY(batch_id) REFERENCES batches(id) ON DELETE RESTRICT,
    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_stock_balances_batch_wh ON stock_balances(batch_id, warehouse_id);
CREATE INDEX IF NOT EXISTS idx_stock_balances_prod_wh ON stock_balances(product_id, warehouse_id);

CREATE TABLE IF NOT EXISTS stock_movements (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    warehouse_id TEXT NOT NULL,
    batch_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    movement_type INTEGER NOT NULL,
    quantity REAL NOT NULL,
    reference_type TEXT NOT NULL,
    reference_id TEXT NOT NULL,
    unit_cost REAL NOT NULL,
    user_id TEXT NOT NULL,
    device_id TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_movements_batch_date ON stock_movements(batch_id, created_at);
CREATE INDEX IF NOT EXISTS idx_movements_prod_date ON stock_movements(product_id, created_at);

CREATE TABLE IF NOT EXISTS sales (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    counter_id TEXT NOT NULL,
    warehouse_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    device_id TEXT NOT NULL,
    invoice_no TEXT NOT NULL,
    invoice_date TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 0,
    customer_id TEXT,
    customer_name TEXT,
    subtotal REAL NOT NULL DEFAULT 0,
    discount_amount REAL NOT NULL DEFAULT 0,
    tax_amount REAL NOT NULL DEFAULT 0,
    round_off REAL NOT NULL DEFAULT 0,
    total REAL NOT NULL DEFAULT 0,
    is_interstate INTEGER NOT NULL DEFAULT 0,
    prescription_ref TEXT,
    notes TEXT,
    created_at TEXT NOT NULL,
    posted_at TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_sales_org_branch_inv ON sales(org_id, branch_id, invoice_no);
CREATE INDEX IF NOT EXISTS idx_sales_date ON sales(invoice_date);

CREATE TABLE IF NOT EXISTS sale_items (
    id TEXT PRIMARY KEY,
    sale_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    product_name TEXT NOT NULL,
    batch_id TEXT NOT NULL,
    batch_number TEXT NOT NULL,
    expiry_date TEXT NOT NULL,
    quantity REAL NOT NULL,
    unit_price REAL NOT NULL,
    mrp REAL NOT NULL,
    discount_pct REAL NOT NULL DEFAULT 0,
    discount_amount REAL NOT NULL DEFAULT 0,
    taxable_amount REAL NOT NULL,
    cgst_rate REAL NOT NULL DEFAULT 0,
    cgst_amount REAL NOT NULL DEFAULT 0,
    sgst_rate REAL NOT NULL DEFAULT 0,
    sgst_amount REAL NOT NULL DEFAULT 0,
    igst_rate REAL NOT NULL DEFAULT 0,
    igst_amount REAL NOT NULL DEFAULT 0,
    net_amount REAL NOT NULL,
    FOREIGN KEY(sale_id) REFERENCES sales(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_sale_items_sale ON sale_items(sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_items_prod ON sale_items(product_id);

CREATE TABLE IF NOT EXISTS sale_payments (
    id TEXT PRIMARY KEY,
    sale_id TEXT NOT NULL,
    payment_mode INTEGER NOT NULL,
    amount REAL NOT NULL,
    reference TEXT,
    paid_at TEXT NOT NULL,
    FOREIGN KEY(sale_id) REFERENCES sales(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_sale_payments_sale ON sale_payments(sale_id);

CREATE TABLE IF NOT EXISTS outbox_events (
    id TEXT PRIMARY KEY,
    aggregate_type TEXT NOT NULL,
    aggregate_id TEXT NOT NULL,
    event_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    device_id TEXT NOT NULL,
    operation_id TEXT NOT NULL,
    created_at TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 0,
    retry_count INTEGER NOT NULL DEFAULT 0,
    synced_at TEXT,
    error_message TEXT
);

CREATE INDEX IF NOT EXISTS idx_outbox_status_date ON outbox_events(status, created_at);
CREATE UNIQUE INDEX IF NOT EXISTS idx_outbox_op_device ON outbox_events(operation_id, device_id);

CREATE TABLE IF NOT EXISTS document_sequences (
    id TEXT PRIMARY KEY,
    org_id TEXT NOT NULL,
    branch_id TEXT NOT NULL,
    doc_type TEXT NOT NULL,
    prefix TEXT NOT NULL,
    last_sequence_number INTEGER NOT NULL DEFAULT 0,
    current_year INTEGER NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_doc_seq_unique ON document_sequences(org_id, branch_id, doc_type, current_year);

CREATE VIRTUAL TABLE IF NOT EXISTS fts_products USING fts5(
    product_id UNINDEXED,
    name,
    brand_name,
    generic_name,
    composition,
    manufacturer_name,
    barcode,
    tokenize = 'porter unicode61'
);

CREATE TRIGGER IF NOT EXISTS trg_products_fts_insert AFTER INSERT ON products
BEGIN
    INSERT INTO fts_products(product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
    VALUES (new.id, new.name, new.brand_name, new.generic_name, new.composition, IFNULL(new.manufacturer_name, ''), IFNULL(new.primary_barcode, ''));
END;

CREATE TRIGGER IF NOT EXISTS trg_products_fts_update AFTER UPDATE ON products
BEGIN
    DELETE FROM fts_products WHERE product_id = old.id;
    INSERT INTO fts_products(product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
    VALUES (new.id, new.name, new.brand_name, new.generic_name, new.composition, IFNULL(new.manufacturer_name, ''), IFNULL(new.primary_barcode, ''));
END;

CREATE TRIGGER IF NOT EXISTS trg_products_fts_delete AFTER DELETE ON products
BEGIN
    DELETE FROM fts_products WHERE product_id = old.id;
END;
";

    private static string GetScheduleDrugsSchemaSql()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resourceName = "Medistock.Infrastructure.Data.Migrations.002_InventoryAndScheduleDrugs.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "002_InventoryAndScheduleDrugs.sql");
        if (File.Exists(localPath))
        {
            return File.ReadAllText(localPath);
        }

        return ScheduleDrugsSqlSchema;
    }

    private const string ScheduleDrugsSqlSchema = @"
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
";

    private static string GetPurchasesSchemaSql()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resourceName = "Medistock.Infrastructure.Data.Migrations.003_PurchasesAndSuppliers.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "003_PurchasesAndSuppliers.sql");
        if (File.Exists(localPath))
        {
            return File.ReadAllText(localPath);
        }

        return PurchasesSqlSchema;
    }

    private const string PurchasesSqlSchema = @"
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
";
}


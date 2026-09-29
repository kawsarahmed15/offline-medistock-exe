"""
Indian Medicine Data Seeder & Transformer Script
Transforms raw JSON data (250,000+ medicines) for Medistock SQLite database or seeder formats.
"""
import sys
import json
import os
import re
import sqlite3
import time
from datetime import datetime

DEFAULT_JSON_PATH = r"c:\Users\ahmed\Downloads\indian_medicine_data.json"
DEFAULT_DB_PATH = os.path.expandvars(r"%LOCALAPPDATA%\Medistock\medistock_local.db")

DOSAGE_MAP = [
    (r"\b(tablet|tab|tabs|caplet)\b", 0, "TAB"),          # Tablet
    (r"\b(capsule|cap|caps)\b", 1, "CAP"),                # Capsule
    (r"\b(syrup|syr|oral solution|elixir|liquid)\b", 2, "BTL"), # Syrup
    (r"\b(injection|inj|vial|ampoule|infusion|iv)\b", 3, "VIAL"), # Injection
    (r"\b(cream)\b", 4, "TUBE"),                          # Cream
    (r"\b(ointment|oint)\b", 5, "TUBE"),                  # Ointment
    (r"\b(gel)\b", 6, "TUBE"),                            # Gel
    (r"\b(drops?|eye drop|ear drop|nasal drop)\b", 7, "BTL"),  # Drops
    (r"\b(inhaler|rotacap|respule|inhalation)\b", 8, "INH"), # Inhaler
    (r"\b(suspension|susp)\b", 9, "BTL"),                 # Suspension
    (r"\b(powder|sachet|granules)\b", 10, "SCT"),         # Powder / Sachet
    (r"\b(spray)\b", 11, "BTL"),                          # Spray
    (r"\b(patch|transdermal)\b", 12, "PCS"),              # Patch
]

SCHEDULE_H1_KEYWORDS = {
    "alprazolam", "clobazam", "clonazepam", "diazepam", "lorazepam", "midazolam",
    "nitrazepam", "oxazepam", "zolpidem", "tramadol", "codeine", "buprenorphine",
    "pentazocine", "meropenem", "imipenem", "ertapenem", "doripenem", "colistin",
    "tigecycline", "balofloxacin", "gemifloxacin", "moxifloxacin", "gatifloxacin"
}

COLD_CHAIN_KEYWORDS = {
    "insulin", "mixtard", "novorapid", "lantus", "humalog", "apidra",
    "erythropoietin", "vaccine", "tetanus", "rabies", "immunoglobulin"
}

def parse_dosage_and_pack(name: str, pack_label: str):
    full_str = f"{name} {pack_label}".lower()
    
    dosage_form = 0 # Default Tablet
    base_unit = "TAB"
    
    for pattern, form_id, unit in DOSAGE_MAP:
        if re.search(pattern, full_str):
            dosage_form = form_id
            base_unit = unit
            break

    # Parse pack units
    pack_units = 10
    if pack_label:
        pl_lower = pack_label.lower()
        # Look for "strip of 10", "box of 100", "bottle of 100 ml", etc.
        m_strip = re.search(r'(?:strip|box|blister|pack)\s+of\s+(\d+)', pl_lower)
        if m_strip:
            try:
                pack_units = max(1, int(m_strip.group(1)))
            except:
                pack_units = 10
        elif dosage_form in (2, 3, 4, 5, 6, 7, 8, 9, 11): # Liquid/Tube/Vial defaults to 1
            pack_units = 1
        else:
            m_num = re.search(r'(\d+)\s*(?:tablet|capsule|tab|cap|sachet)', pl_lower)
            if m_num:
                try:
                    pack_units = max(1, int(m_num.group(1)))
                except:
                    pack_units = 10

    return dosage_form, pack_units, base_unit

def determine_schedule_and_flags(name: str, comp: str):
    text = f"{name} {comp}".lower()
    
    is_cold_chain = any(kw in text for kw in COLD_CHAIN_KEYWORDS)
    is_h1 = any(kw in text for kw in SCHEDULE_H1_KEYWORDS)
    
    if is_h1:
        schedule = 2 # Schedule H1
        rx = 1
    elif "paracetamol" in text or "antacid" in text or "cough" in text or "vitamin" in text or "calcium" in text:
        schedule = 0 # OTC
        rx = 0
    else:
        schedule = 1 # Schedule H
        rx = 1

    return schedule, rx, (1 if is_cold_chain else 0), 0

def transform_records(raw_data, org_id="org-1"):
    now_iso = datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")
    products = []

    for item in raw_data:
        raw_id = item.get("id")
        prod_id = f"in_med_{raw_id}"
        name = (item.get("name") or "").strip()
        if not name:
            continue

        mfg = (item.get("manufacturer_name") or "").strip()
        comp1 = (item.get("short_composition1") or "").strip()
        comp2 = (item.get("short_composition2") or "").strip()
        
        comp_parts = [c for c in (comp1, comp2) if c]
        composition = " + ".join(comp_parts)
        
        is_discontinued = (item.get("Is_discontinued") or "").strip().upper() == "TRUE"
        is_active = 0 if is_discontinued else 1

        dosage_form, pack_units, base_unit = parse_dosage_and_pack(name, item.get("pack_size_label") or "")
        schedule, rx, cold_chain, narcotic = determine_schedule_and_flags(name, composition)

        try:
            mrp = float(item.get("price(\u20b9)") or 0.0)
        except:
            mrp = 0.0

        products.append({
            "id": prod_id,
            "org_id": org_id,
            "name": name,
            "brand_name": name,
            "generic_name": composition if composition else name,
            "composition": composition,
            "strength": "",
            "dosage_form": dosage_form,
            "pack_units": pack_units,
            "base_unit": base_unit,
            "hsn_code": "3004",
            "gst_rate_percent": 12.0,
            "schedule": schedule,
            "is_prescription_required": rx,
            "is_cold_chain": cold_chain,
            "is_narcotic": narcotic,
            "is_active": is_active,
            "min_stock_alert": 10.0,
            "primary_barcode": None,
            "manufacturer_name": mfg,
            "created_at": now_iso,
            "mrp": mrp
        })

    return products

def seed_to_sqlite(json_path, db_path, org_id="org-1"):
    if not os.path.exists(json_path):
        print(f"Error: JSON file not found: {json_path}")
        return False

    print(f"Loading data from {json_path}...")
    t0 = time.time()
    with open(json_path, "r", encoding="utf-8") as f:
        raw_data = json.load(f)
    print(f"Loaded {len(raw_data):,} records in {time.time()-t0:.2f}s")

    print("Transforming records...")
    t1 = time.time()
    products = transform_records(raw_data, org_id)
    print(f"Transformed {len(products):,} products in {time.time()-t1:.2f}s")

    os.makedirs(os.path.dirname(db_path), exist_ok=True)
    conn = sqlite3.connect(db_path)
    conn.execute("PRAGMA journal_mode = WAL;")
    conn.execute("PRAGMA synchronous = NORMAL;")

    # Drop triggers temporarily for maximum speed
    conn.execute("DROP TRIGGER IF EXISTS trg_products_fts_insert;")
    conn.execute("DROP TRIGGER IF EXISTS trg_products_fts_update;")
    conn.execute("DROP TRIGGER IF EXISTS trg_products_fts_delete;")

    print(f"Inserting into SQLite database at {db_path}...")
    t2 = time.time()
    
    rows = [
        (
            p["id"], p["org_id"], p["name"], p["brand_name"], p["generic_name"],
            p["composition"], p["strength"], p["dosage_form"], p["pack_units"],
            p["base_unit"], p["hsn_code"], p["gst_rate_percent"], p["schedule"],
            p["is_prescription_required"], p["is_cold_chain"], p["is_narcotic"],
            p["is_active"], p["min_stock_alert"], p["primary_barcode"],
            p["manufacturer_name"], p["created_at"], None
        )
        for p in products
    ]

    conn.executemany("""
    INSERT OR REPLACE INTO products (
        id, org_id, name, brand_name, generic_name, composition, strength,
        dosage_form, pack_units, base_unit, hsn_code, gst_rate_percent,
        schedule, is_prescription_required, is_cold_chain, is_narcotic,
        is_active, min_stock_alert, primary_barcode, manufacturer_name,
        created_at, updated_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, rows)
    conn.commit()
    print(f"✓ Inserted {len(rows):,} products in {time.time()-t2:.2f}s")

    # Rebuild FTS5 index
    print("Rebuilding FTS5 Search Index...")
    t3 = time.time()
    conn.execute("DELETE FROM fts_products;")
    conn.execute("""
    INSERT INTO fts_products (product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
    SELECT id, name, brand_name, generic_name, composition, IFNULL(manufacturer_name, ''), IFNULL(primary_barcode, '')
    FROM products;
    """)
    conn.commit()
    print(f"✓ FTS5 search index rebuilt in {time.time()-t3:.2f}s")

    # Recreate Triggers
    conn.execute("""
    CREATE TRIGGER IF NOT EXISTS trg_products_fts_insert AFTER INSERT ON products
    BEGIN
        INSERT INTO fts_products(product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
        VALUES (new.id, new.name, new.brand_name, new.generic_name, new.composition, IFNULL(new.manufacturer_name, ''), IFNULL(new.primary_barcode, ''));
    END;
    """)
    conn.execute("""
    CREATE TRIGGER IF NOT EXISTS trg_products_fts_update AFTER UPDATE ON products
    BEGIN
        DELETE FROM fts_products WHERE product_id = old.id;
        INSERT INTO fts_products(product_id, name, brand_name, generic_name, composition, manufacturer_name, barcode)
        VALUES (new.id, new.name, new.brand_name, new.generic_name, new.composition, IFNULL(new.manufacturer_name, ''), IFNULL(new.primary_barcode, ''));
    END;
    """)
    conn.execute("""
    CREATE TRIGGER IF NOT EXISTS trg_products_fts_delete AFTER DELETE ON products
    BEGIN
        DELETE FROM fts_products WHERE product_id = old.id;
    END;
    """)
    conn.commit()
    conn.close()

    print(f"🎉 Seeding Complete! Total time: {time.time()-t0:.2f} seconds")
    return True

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Medistock Indian Medicine Data Seeder")
    parser.add_argument("--json", default=DEFAULT_JSON_PATH, help="Source JSON file path")
    parser.add_argument("--db", default=DEFAULT_DB_PATH, help="Target SQLite DB path")
    parser.add_argument("--org", default="org-1", help="Organization ID")
    
    args = parser.parse_args()
    seed_to_sqlite(args.json, args.db, args.org)

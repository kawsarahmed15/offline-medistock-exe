"""
Analyze Indian Medicine Dataset (JSON)
Reports count, types, pricing, manufacturer, packaging, and compositions.
"""
import sys
import json
import os
import re
from collections import Counter

sys.stdout.reconfigure(encoding='utf-8')

DEFAULT_PATH = r"c:\Users\ahmed\Downloads\indian_medicine_data.json"

def analyze_dataset(file_path=DEFAULT_PATH):
    if not os.path.exists(file_path):
        print(f"Error: File not found at {file_path}")
        return

    print("=" * 70)
    print("MEDISTOCK - INDIAN MEDICINE DATASET ANALYZER")
    print("=" * 70)
    print(f"File Path: {file_path}")
    file_size_mb = os.path.getsize(file_path) / (1024 * 1024)
    print(f"File Size: {file_size_mb:.2f} MB")
    print("Loading JSON data into memory...")

    with open(file_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    total_count = len(data)
    print(f"\n[+] Total Medicine Products: {total_count:,}")

    types_counter = Counter()
    manufacturers = Counter()
    discontinued_counter = Counter()
    prices = []
    pack_size_counter = Counter()
    dosage_form_counter = Counter()
    missing_fields = Counter()
    duplicate_names = Counter()
    seen_ids = set()
    duplicate_ids = 0

    dosage_keywords = {
        "Tablet": ["tablet", "tab", "caplet"],
        "Capsule": ["capsule", "cap"],
        "Syrup": ["syrup", "syr", "liquid", "oral solution", "elixir"],
        "Injection": ["injection", "inj", "vial", "ampoule", "infusion", "iv"],
        "Suspension": ["suspension", "susp"],
        "Drops": ["drop", "drops", "eye drop", "ear drop", "nasal drop"],
        "Ointment": ["ointment", "oint"],
        "Cream": ["cream"],
        "Gel": ["gel"],
        "Inhaler": ["inhaler", "rotacap", "respule", "inhalation"],
        "Powder": ["powder", "sachet", "granules"],
        "Spray": ["spray"],
        "Patch": ["patch", "transdermal"],
    }

    for item in data:
        med_id = item.get("id")
        if med_id in seen_ids:
            duplicate_ids += 1
        else:
            seen_ids.add(med_id)

        name = (item.get("name") or "").strip()
        if name:
            duplicate_names[name.lower()] += 1
        else:
            missing_fields["empty_name"] += 1

        price_val = item.get("price(\u20b9)")
        discontinued = (item.get("Is_discontinued") or "").strip().upper()
        mfg = (item.get("manufacturer_name") or "").strip()
        m_type = (item.get("type") or "").strip().lower()
        pack_label = (item.get("pack_size_label") or "").strip()
        comp1 = (item.get("short_composition1") or "").strip()
        comp2 = (item.get("short_composition2") or "").strip()

        types_counter[m_type if m_type else "unknown"] += 1
        manufacturers[mfg if mfg else "unknown"] += 1
        discontinued_counter[discontinued if discontinued else "FALSE"] += 1
        pack_size_counter[pack_label if pack_label else "unknown"] += 1

        # Classify dosage form from name and pack label
        full_text = f"{name} {pack_label}".lower()
        matched_form = "Other"
        for form, keywords in dosage_keywords.items():
            if any(k in full_text for k in keywords):
                matched_form = form
                break
        dosage_form_counter[matched_form] += 1

        try:
            p = float(price_val)
            if p > 0:
                prices.append(p)
            else:
                missing_fields["zero_price"] += 1
        except (ValueError, TypeError):
            missing_fields["invalid_price"] += 1

        if not mfg:
            missing_fields["empty_manufacturer"] += 1
        if not comp1 and not comp2:
            missing_fields["empty_composition"] += 1

    print("\n" + "-" * 40)
    print("1. MEDICINE TYPES BREAKDOWN:")
    print("-" * 40)
    for t, c in types_counter.most_common():
        print(f"  • {t.title():<20}: {c:>8,} ({c/total_count*100:>5.2f}%)")

    print("\n" + "-" * 40)
    print("2. ESTIMATED DOSAGE FORMS:")
    print("-" * 40)
    for df, c in dosage_form_counter.most_common():
        print(f"  • {df:<20}: {c:>8,} ({c/total_count*100:>5.2f}%)")

    print("\n" + "-" * 40)
    print("3. DISCONTINUED STATUS:")
    print("-" * 40)
    for d, c in discontinued_counter.items():
        label = "Active" if d == "FALSE" else "Discontinued"
        print(f"  • {label:<20}: {c:>8,} ({c/total_count*100:>5.2f}%)")

    print("\n" + "-" * 40)
    print("4. PRICING ANALYSIS (MRP in INR):")
    print("-" * 40)
    if prices:
        sorted_p = sorted(prices)
        p_len = len(sorted_p)
        print(f"  • Total Products with Price > 0 : {p_len:>8,} ({p_len/total_count*100:.1f}%)")
        print(f"  • Minimum Price                 : ₹{min(prices):>8.2f}")
        print(f"  • Maximum Price                 : ₹{max(prices):>8.2f}")
        print(f"  • Average Price                 : ₹{sum(prices)/len(prices):>8.2f}")
        print(f"  • Median Price                  : ₹{sorted_p[p_len//2]:>8.2f}")
        print(f"  • 25th Percentile Price         : ₹{sorted_p[int(p_len*0.25)]:>8.2f}")
        print(f"  • 75th Percentile Price         : ₹{sorted_p[int(p_len*0.75)]:>8.2f}")
        print(f"  • 90th Percentile Price         : ₹{sorted_p[int(p_len*0.90)]:>8.2f}")

    print("\n" + "-" * 40)
    print(f"5. MANUFACTURER SUMMARY (Unique Brands/Companies: {len(manufacturers):,}):")
    print("-" * 40)
    print("  Top 20 Pharma Companies by Product Count:")
    for idx, (m, c) in enumerate(manufacturers.most_common(20), 1):
        print(f"  {idx:>2}. {m:<45}: {c:>6,} products ({c/total_count*100:.2f}%)")

    print("\n" + "-" * 40)
    print("6. TOP 15 PACKAGING PATTERNS:")
    print("-" * 40)
    for idx, (p, c) in enumerate(pack_size_counter.most_common(15), 1):
        print(f"  {idx:>2}. {p:<45}: {c:>6,} products")

    print("\n" + "-" * 40)
    print("7. DATA QUALITY & INTEGRITY METRICS:")
    print("-" * 40)
    print(f"  • Duplicate IDs                : {duplicate_ids:,}")
    dup_names_count = sum(1 for c in duplicate_names.values() if c > 1)
    print(f"  • Names with duplicate entries : {dup_names_count:,}")
    print(f"  • Missing Composition          : {missing_fields['empty_composition']:,} ({missing_fields['empty_composition']/total_count*100:.2f}%)")
    print(f"  • Missing Manufacturer         : {missing_fields['empty_manufacturer']:,} ({missing_fields['empty_manufacturer']/total_count*100:.2f}%)")
    print(f"  • Missing/Zero Price           : {missing_fields['zero_price'] + missing_fields['invalid_price']:,}")
    print("=" * 70)

if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_PATH
    analyze_dataset(target)

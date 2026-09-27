# 🧾 Medistock Bill & Invoice Template Guide

Welcome to the **Medistock Bill Template Engine**. This guide explains the complete JSON schema, all available parameters, styling configurations, paper dimensions, and how to create, test, export, and import custom invoice templates.

---

## 📑 Table of Contents
1. [Overview & Architecture](#overview--architecture)
2. [Supported Paper Sizes](#supported-paper-sizes)
3. [JSON Schema & Parameter Reference](#json-schema--parameter-reference)
   - [Root Configuration](#root-configuration)
   - [Styling (`styling`)](#styling-styling)
   - [Header Section (`header`)](#header-section-header)
   - [Metadata & Patient Info (`metadata`)](#metadata--patient-info-metadata)
   - [Column Customization (`columns`)](#column-customization-columns)
   - [GST Tax Summary Table (`taxSummary`)](#gst-tax-summary-table-taxsummary)
   - [Totals & Footer Section (`footer`)](#totals--footer-section-footer)
   - [Signatures Section (`signatures`)](#signatures-signatures)
4. [Available Column Field Types (`BillFieldType`)](#available-column-field-types-billfieldtype)
5. [How to Import & Export Templates in Medistock](#how-to-import--export-templates-in-medistock)
6. [Included Library Templates](#included-library-templates)

---

## 1. Overview & Architecture

Medistock uses a **Unified Document Generation Pipeline** (`IBillDocumentGenerator`). Every template config (`BillTemplateConfig`) defined as JSON is rendered identically across:
1. **Interactive WYSIWYG Live Preview** (WebView2 with instant 0-lag updates on typing)
2. **Direct Hardware Thermal & Laser Printing** (Windows Spooler / Driver)
3. **High-Resolution Vector PDF Export** (Chromium Core Print-to-PDF)

---

## 2. Supported Paper Sizes

The `paperSize` parameter configures the exact printable dimensions and CSS `@page` layout rules:

| Paper Size Enum | Width / Height | Target Printers & Use Cases |
| :--- | :--- | :--- |
| `A4_Portrait` | 210mm × 297mm | Standard Laser/Inkjet full-page GST Tax Invoices |
| `A4_Landscape` | 297mm × 210mm | Wide multi-column wholesale/distributor invoices |
| `A5_Landscape` | 210mm × 148mm | Half-page medical memos (laser, inkjet, or 24-pin dot matrix) |
| `A5_Portrait` | 148mm × 210mm | Vertical prescription memo & receipt |
| `Thermal_80mm` | 80mm roll (3 inch) | Standard POS receipt printers (Epson, TVS, NGX, Citizen) |
| `Thermal_58mm` | 58mm roll (2 inch) | Mini portable & mobile POS thermal printers |
| `Custom` | Configurable margins | Custom stationery with pre-printed letterheads |

---

## 3. JSON Schema & Parameter Reference

### Root Configuration

```json
{
  "id": "custom_template_id_123",
  "name": "My Custom Pharmacy Invoice",
  "isDefault": false,
  "paperSize": "A4_Portrait",
  "styling": { ... },
  "header": { ... },
  "metadata": { ... },
  "columns": [ ... ],
  "taxSummary": { ... },
  "footer": { ... },
  "signatures": { ... }
}
```

- **`id`** (`string`): Unique identifier (GUID or slug).
- **`name`** (`string`): Human-readable title displayed in the template selector dropdown.
- **`isDefault`** (`boolean`): If `true`, this template is automatically used at POS checkout and print preview.
- **`paperSize`** (`string`): One of `A4_Portrait`, `A4_Landscape`, `A5_Portrait`, `A5_Landscape`, `Thermal_80mm`, `Thermal_58mm`, `Custom`.

---

### Styling (`styling`)

Controls typography, margins, accents, and table borders.

```json
"styling": {
  "fontFamily": "'Segoe UI', 'Helvetica Neue', Arial, sans-serif",
  "baseFontSizePt": 9.5,
  "marginTopMm": 8.0,
  "marginBottomMm": 8.0,
  "marginLeftMm": 8.0,
  "marginRightMm": 8.0,
  "primaryColorHex": "#1b5e20",
  "showTableBorders": true,
  "alternateRowColors": true,
  "density": "Compact"
}
```

- **`fontFamily`** (`string`): CSS font-family stack (e.g., `'Segoe UI', Arial, sans-serif` or `monospace` for thermal slips).
- **`baseFontSizePt`** (`double`): Base font size in points (e.g. `9.5` for A4, `8.5` for A5, `9.0` for 80mm thermal).
- **`marginTopMm`**, **`marginBottomMm`**, **`marginLeftMm`**, **`marginRightMm`** (`double`): Page margins in millimeters.
- **`primaryColorHex`** (`string`): Primary accent color in hex code (e.g. `#1b5e20` green, `#0f766e` teal, `#1e3a8a` navy blue, `#000000` mono).
- **`showTableBorders`** (`boolean`): If `true`, renders grid cell borders.
- **`alternateRowColors`** (`boolean`): If `true`, applies zebra shading to table rows.
- **`density`** (`string`): Row padding density (`"Compact"`, `"Normal"`, `"Comfortable"`).

---

### Header Section (`header`)

Pharmacy branding, store contacts, drug licenses, and statutory titles.

```json
"header": {
  "isVisible": true,
  "storeName": "MEDISTOCK PHARMACY",
  "tagline": "Complete Healthcare, Surgical & Oncology Care",
  "alignment": "Center",
  "addressLine1": "Shop 101, Ground Floor, Royal Complex, MG Road",
  "addressLine2": "Mumbai - 400001, Maharashtra",
  "phone": "+91 98200 54321 / 022-28004455",
  "email": "contact@apexpharmacy.com",
  "showLogo": false,
  "logoBase64OrPath": null,
  "showGstin": true,
  "gstinLabel": "GSTIN",
  "gstin": "27ABCDE1234F1Z5",
  "showDlNumbers": true,
  "dlLabel": "D.L. Nos.",
  "dlNumbers": "MH-MZ2-445566, MH-MZ2-445567 (20B/21B)",
  "showFssai": true,
  "fssaiLabel": "FSSAI Lic No.",
  "fssai": "10019022009876",
  "showPan": false,
  "pan": "",
  "saleInvoiceTitle": "TAX INVOICE",
  "estimateTitle": "BILL OF SUPPLY / ESTIMATE",
  "returnCreditNoteTitle": "CREDIT NOTE / SALES RETURN VOUCHER"
}
```

- **`alignment`** (`string`): `"Left"`, `"Center"`, or `"Right"`.
- **`showGstin`**, **`showDlNumbers`**, **`showFssai`**, **`showPan`** (`boolean`): Toggle specific regulatory license lines on or off.

---

### Metadata & Patient Info (`metadata`)

Metadata grid displayed directly below the header.

```json
"metadata": {
  "showInvoiceNo": true,
  "invoiceNoLabel": "Invoice No",
  "showInvoiceDate": true,
  "invoiceDateLabel": "Date",
  "dateFormat": "dd-MMM-yyyy",
  "showInvoiceTime": true,
  "timeFormat": "hh:mm tt",
  "showCashier": true,
  "cashierLabel": "Billed By",
  "showCounter": true,
  "counterLabel": "Counter",
  "showCustomerName": true,
  "customerNameLabel": "Patient Name",
  "showCustomerPhone": true,
  "customerPhoneLabel": "Mobile",
  "showCustomerAddress": true,
  "customerAddressLabel": "Address",
  "showCustomerGstin": true,
  "customerGstinLabel": "Buyer GSTIN",
  "showDoctorName": true,
  "doctorNameLabel": "Prescribed By",
  "showDoctorRegNo": true,
  "doctorRegNoLabel": "Dr. Reg No",
  "showPrescriptionRef": true,
  "prescriptionRefLabel": "Rx Ref / Date",
  "showPaymentMode": true,
  "paymentModeLabel": "Pay Mode"
}
```

---

### Column Customization (`columns`)

Each object in the `columns` array represents a table column.

```json
{
  "fieldType": "ItemName",
  "headerTitle": "Medicine Description",
  "isVisible": true,
  "displayOrder": 2,
  "widthPercent": 28.0,
  "alignment": "Left"
}
```

- **`fieldType`** (`string`): One of the supported `BillFieldType` enum values (see list below).
- **`headerTitle`** (`string`): Custom column header name.
- **`isVisible`** (`boolean`): If `true`, column is rendered. If `false`, column is hidden.
- **`displayOrder`** (`int`): Sequence index (1, 2, 3...).
- **`widthPercent`** (`double`): Relative percentage width in the table (e.g. `28.0` for 28%).
- **`alignment`** (`string`): `"Left"`, `"Center"`, or `"Right"`.

---

### GST Tax Summary Table (`taxSummary`)

```json
"taxSummary": {
  "showTaxTable": true,
  "showHsnBreakdown": true,
  "showCgstSgstSeparately": true,
  "showIgst": true
}
```

---

### Totals & Footer Section (`footer`)

```json
"footer": {
  "showTotalItems": true,
  "showTotalQuantity": true,
  "showSubtotal": true,
  "showTotalDiscount": true,
  "showSavingsCallout": false,
  "savingsTemplate": "🎉 You saved {SAVINGS_AMOUNT} on this bill!",
  "showRoundOff": true,
  "showGrandTotal": true,
  "showAmountInWords": true,
  "showPaymentBreakdown": true,
  "showBankDetails": false,
  "bankDetailsText": "HDFC Bank | A/C: 50200012345678 | IFSC: HDFC0000123",
  "showUpiQrCode": false,
  "upiId": "pharmacy@upi",
  "upiPayeeName": "Medistock Pharmacy",
  "showTermsAndConditions": true,
  "termsAndConditionsText": "1. Goods once sold will not be returned without original cash memo.\n2. Consult physician before consuming Schedule H/H1 medicines.\n3. Keep medicines away from sunlight and moisture.\n4. Subject to local jurisdiction only.",
  "showGreeting": true,
  "greetingText": "*** THANK YOU & GET WELL SOON ***"
}
```

> [!NOTE]
> `showSavingsCallout` and `showBankDetails` are set to `false` by default. They will only render when explicitly toggled on by the user.

---

### Signatures (`signatures`)

```json
"signatures": {
  "showPharmacistSign": true,
  "pharmacistSignTitle": "Registered Pharmacist",
  "showCustomerSign": false,
  "customerSignTitle": "Receiver's Signature",
  "showAuthorizedSign": true,
  "authorizedSignTitle": "For MEDISTOCK PHARMACY\nAuthorized Signatory"
}
```

---

## 4. Available Column Field Types (`BillFieldType`)

You can include any combination of the following fields in the `columns` array:

| `fieldType` Value | Default Header | Description | Default Alignment |
| :--- | :--- | :--- | :--- |
| `SrNo` | `#` | Auto-incrementing line item number (1, 2, 3...) | `Center` |
| `ItemName` | `Medicine Description` | Product brand name and dosage form | `Left` |
| `Packing` | `Pack` | Packaging unit (e.g., `10s`, `100ml`, `Vial`) | `Left` |
| `Manufacturer` | `Mfg` | Manufacturer code / pharma company | `Left` |
| `BatchNumber` | `Batch` | Medicine manufacturing batch number | `Left` |
| `ExpiryDate` | `Exp` | Expiry date formatted as `MM/yy` or `MMM-yy` | `Center` |
| `HsnCode` | `HSN` | Statutory GST HSN code (e.g., `3004`) | `Center` |
| `Mrp` | `MRP` | Maximum Retail Price | `Right` |
| `UnitRate` | `Rate` | Selling rate per unit / strip | `Right` |
| `Quantity` | `Qty` | Billed quantity | `Right` |
| `FreeQuantity` | `Free` | Promotional free / bonus quantity | `Right` |
| `DiscountPercent` | `Disc%` | Discount percentage applied (e.g., `10.0%`) | `Right` |
| `DiscountAmount` | `Disc ₹` | Itemized discount rupee amount | `Right` |
| `GstPercent` | `GST%` | Applicable GST rate (e.g., `12.0%`) | `Right` |
| `CgstAmount` | `CGST` | Central GST rupee amount | `Right` |
| `SgstAmount` | `SGST` | State GST rupee amount | `Right` |
| `IgstAmount` | `IGST` | Integrated GST rupee amount | `Right` |
| `TaxableAmount` | `Taxable` | Taxable value before GST | `Right` |
| `TotalAmount` | `Amount (₹)` | Net line amount after discount & tax | `Right` |

---

## 5. How to Import & Export Templates in Medistock

### Exporting a Template
1. Open **Settings ➔ Bill Customizer Studio**.
2. Select any template from the dropdown.
3. Click **"📤 Export JSON"**. The complete JSON definition is copied to your clipboard.

### Importing a Custom Template
1. In **Bill Customizer Studio**, click **"📥 Import JSON"**.
2. Paste your custom JSON into the dialog box (or load from any template file).
3. Click **"Import Template"**.
4. The template is immediately saved into the offline SQLite database, added to your template library, and rendered in the live preview.

---

## 6. Included Library Templates

The following sample files are provided in the `templates/` folder:
- [`standard_a4_gst_invoice.json`](file:///D:/Projects/Medistock-offlinefirst/templates/standard_a4_gst_invoice.json): Full-page GST compliant A4 invoice.
- [`compact_a5_memo_landscape.json`](file:///D:/Projects/Medistock-offlinefirst/templates/compact_a5_memo_landscape.json): A5 landscape memo for half-page paper and dot-matrix printers.
- [`thermal_80mm_pos_slip.json`](file:///D:/Projects/Medistock-offlinefirst/templates/thermal_80mm_pos_slip.json): 3-inch high-speed POS thermal slip.

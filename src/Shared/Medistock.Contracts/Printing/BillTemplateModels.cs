using System;
using System.Collections.Generic;

namespace Medistock.Contracts.Printing;

public enum PaperSize
{
    A4_Portrait,
    A4_Landscape,
    A5_Portrait,
    A5_Landscape,
    Thermal_80mm,
    Thermal_58mm,
    Custom
}

public enum TextAlignmentOption
{
    Left,
    Center,
    Right
}

public enum PrinterInterfaceType
{
    WindowsSpoolerRaw,
    WindowsSystemDriver,
    NetworkTcp,
    SerialCom
}

public enum BillFieldType
{
    SrNo,
    ItemName,
    Packing,
    Manufacturer,
    BatchNumber,
    ExpiryDate,
    HsnCode,
    Mrp,
    UnitRate,
    Quantity,
    FreeQuantity,
    DiscountPercent,
    DiscountAmount,
    GstPercent,
    CgstAmount,
    SgstAmount,
    IgstAmount,
    TaxableAmount,
    TotalAmount
}

public class BillTemplateConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Standard A4 Tax Invoice";
    public bool IsDefault { get; set; } = true;
    public PaperSize PaperSize { get; set; } = PaperSize.A4_Portrait;

    public BillStyleConfig Styling { get; set; } = new();
    public BillHeaderConfig Header { get; set; } = new();
    public BillMetadataConfig Metadata { get; set; } = new();
    public List<BillColumnConfig> Columns { get; set; } = new();
    public BillFooterConfig Footer { get; set; } = new();
    public BillTaxSummaryConfig TaxSummary { get; set; } = new();
    public BillSignatureConfig Signatures { get; set; } = new();
}

public class BillStyleConfig
{
    public string FontFamily { get; set; } = "'Segoe UI', 'Helvetica Neue', Arial, sans-serif";
    public double BaseFontSizePt { get; set; } = 10.0;
    public double MarginTopMm { get; set; } = 8.0;
    public double MarginBottomMm { get; set; } = 8.0;
    public double MarginLeftMm { get; set; } = 8.0;
    public double MarginRightMm { get; set; } = 8.0;
    public string PrimaryColorHex { get; set; } = "#1b5e20"; // Pharmacy Green
    public bool ShowTableBorders { get; set; } = true;
    public bool AlternateRowColors { get; set; } = true;
    public string Density { get; set; } = "Compact"; // Compact, Normal, Comfortable
}

public class BillHeaderConfig
{
    public bool IsVisible { get; set; } = true;
    public string StoreName { get; set; } = "MEDISTOCK PHARMACY";
    public string Tagline { get; set; } = "Complete Healthcare & Surgical Centre";
    public TextAlignmentOption Alignment { get; set; } = TextAlignmentOption.Center;
    public string AddressLine1 { get; set; } = "123, Healthcare Avenue, Opp. Civil Hospital";
    public string AddressLine2 { get; set; } = "MG Road, Mumbai - 400001, Maharashtra";
    public string Phone { get; set; } = "+91 98765 43210 / 022-28001122";
    public string Email { get; set; } = "contact@medistockpharmacy.com";

    public bool ShowLogo { get; set; } = false;
    public string? LogoBase64OrPath { get; set; }
    public double LogoMaxWidthPx { get; set; } = 120;
    public double LogoMaxHeightPx { get; set; } = 60;

    public bool ShowGstin { get; set; } = true;
    public string GstinLabel { get; set; } = "GSTIN";
    public string Gstin { get; set; } = "27AAAAA0000A1Z5";

    public bool ShowDlNumbers { get; set; } = true;
    public string DlLabel { get; set; } = "D.L. Nos.";
    public string DlNumbers { get; set; } = "MH-MZ2-123456, MH-MZ2-123457 (20B/21B)";

    public bool ShowFssai { get; set; } = true;
    public string FssaiLabel { get; set; } = "FSSAI Lic No.";
    public string Fssai { get; set; } = "10019022009876";

    public bool ShowPan { get; set; } = false;
    public string Pan { get; set; } = "";

    public string SaleInvoiceTitle { get; set; } = "TAX INVOICE";
    public string EstimateTitle { get; set; } = "BILL OF SUPPLY / ESTIMATE";
    public string ReturnCreditNoteTitle { get; set; } = "CREDIT NOTE / SALES RETURN VOUCHER";

    public bool ShowCompositionDeclaration { get; set; } = false;
    public string CompositionDeclarationText { get; set; } = "Composition Taxable Person — Not eligible to collect tax on supplies.";
}

public static class BillTemplateJsonSerializer
{
    public static readonly System.Text.Json.JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static string Serialize(BillTemplateConfig config)
        => System.Text.Json.JsonSerializer.Serialize(config, DefaultOptions);

    public static BillTemplateConfig? Deserialize(string json)
        => System.Text.Json.JsonSerializer.Deserialize<BillTemplateConfig>(json, DefaultOptions);
}

public class BillMetadataConfig
{
    public bool ShowInvoiceNo { get; set; } = true;
    public string InvoiceNoLabel { get; set; } = "Invoice No";

    public bool ShowInvoiceDate { get; set; } = true;
    public string InvoiceDateLabel { get; set; } = "Date";
    public string DateFormat { get; set; } = "dd-MMM-yyyy";

    public bool ShowInvoiceTime { get; set; } = true;
    public string TimeFormat { get; set; } = "hh:mm tt";

    public bool ShowCashier { get; set; } = true;
    public string CashierLabel { get; set; } = "Billed By";

    public bool ShowCounter { get; set; } = true;
    public string CounterLabel { get; set; } = "Counter";

    public bool ShowCustomerName { get; set; } = true;
    public string CustomerNameLabel { get; set; } = "Patient Name";

    public bool ShowCustomerPhone { get; set; } = true;
    public string CustomerPhoneLabel { get; set; } = "Mobile";

    public bool ShowCustomerAddress { get; set; } = true;
    public string CustomerAddressLabel { get; set; } = "Address";

    public bool ShowCustomerGstin { get; set; } = true;
    public string CustomerGstinLabel { get; set; } = "Buyer GSTIN";

    public bool ShowDoctorName { get; set; } = true;
    public string DoctorNameLabel { get; set; } = "Prescribed By";

    public bool ShowDoctorRegNo { get; set; } = true;
    public string DoctorRegNoLabel { get; set; } = "Dr. Reg No";

    public bool ShowPrescriptionRef { get; set; } = true;
    public string PrescriptionRefLabel { get; set; } = "Rx Ref / Date";

    public bool ShowPaymentMode { get; set; } = true;
    public string PaymentModeLabel { get; set; } = "Pay Mode";
}

public class BillColumnConfig
{
    public BillFieldType FieldType { get; set; }
    public string HeaderTitle { get; set; } = "";
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }
    public double WidthPercent { get; set; }
    public TextAlignmentOption Alignment { get; set; } = TextAlignmentOption.Left;
}

public class BillTaxSummaryConfig
{
    public bool ShowTaxTable { get; set; } = true;
    public bool ShowHsnBreakdown { get; set; } = true;
    public bool ShowCgstSgstSeparately { get; set; } = true;
    public bool ShowIgst { get; set; } = true;
}

public class BillFooterConfig
{
    public bool ShowTotalItems { get; set; } = true;
    public bool ShowTotalQuantity { get; set; } = true;
    public bool ShowSubtotal { get; set; } = true;
    public bool ShowTotalDiscount { get; set; } = true;
    public bool ShowSavingsCallout { get; set; } = false;
    public string SavingsTemplate { get; set; } = string.Empty;

    public bool ShowRoundOff { get; set; } = true;
    public bool ShowGrandTotal { get; set; } = true;
    public bool ShowAmountInWords { get; set; } = true;

    public bool ShowPaymentBreakdown { get; set; } = true;
    public bool ShowBankDetails { get; set; } = false;
    public string BankDetailsText { get; set; } = string.Empty;

    public bool ShowUpiQrCode { get; set; } = true;
    public string UpiId { get; set; } = "pharmacy@upi";
    public string UpiPayeeName { get; set; } = "Medistock Pharmacy";

    public bool ShowTermsAndConditions { get; set; } = true;
    public string TermsAndConditionsText { get; set; } =
        "1. Goods once sold will not be returned without original cash memo.\n" +
        "2. Consult physician before consuming Schedule H/H1 medicines.\n" +
        "3. Keep medicines away from sunlight and moisture.\n" +
        "4. Subject to local jurisdiction only.";

    public bool ShowGreeting { get; set; } = true;
    public string GreetingText { get; set; } = "*** THANK YOU & GET WELL SOON ***";
}

public class BillSignatureConfig
{
    public bool ShowPharmacistSign { get; set; } = true;
    public string PharmacistSignTitle { get; set; } = "Registered Pharmacist";

    public bool ShowCustomerSign { get; set; } = false;
    public string CustomerSignTitle { get; set; } = "Receiver's Signature";

    public bool ShowAuthorizedSign { get; set; } = true;
    public string AuthorizedSignTitle { get; set; } = "For MEDISTOCK PHARMACY\nAuthorized Signatory";
}

public record ReceiptItemModel(
    string ProductName,
    string BatchNumber,
    DateTime ExpiryDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetAmount,
    decimal GstPercent
);

public record ReceiptPaymentModel(
    string Mode,
    decimal Amount,
    string? Reference
);

public record SaleReceiptModel(
    string PharmacyName,
    string PharmacyAddress,
    string PharmacyPhone,
    string Gstin,
    string DlNumbers,
    string InvoiceNo,
    DateTime InvoiceDate,
    string CounterName,
    string CashierName,
    string? CustomerName,
    string? DoctorName,
    List<ReceiptItemModel> Items,
    decimal Subtotal,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal RoundOff,
    decimal GrandTotal,
    List<ReceiptPaymentModel> Payments
);

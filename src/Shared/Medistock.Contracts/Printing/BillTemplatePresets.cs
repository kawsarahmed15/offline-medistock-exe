using System;
using System.Collections.Generic;

namespace Medistock.Contracts.Printing;

public static class BillTemplatePresets
{
    public static List<BillColumnConfig> GetAllAvailableColumns()
    {
        return new List<BillColumnConfig>
        {
            new() { FieldType = BillFieldType.SrNo, HeaderTitle = "#", IsVisible = true, DisplayOrder = 1, WidthPercent = 4, Alignment = TextAlignmentOption.Center },
            new() { FieldType = BillFieldType.ItemName, HeaderTitle = "Medicine Description", IsVisible = true, DisplayOrder = 2, WidthPercent = 28, Alignment = TextAlignmentOption.Left },
            new() { FieldType = BillFieldType.Packing, HeaderTitle = "Pack", IsVisible = true, DisplayOrder = 3, WidthPercent = 6, Alignment = TextAlignmentOption.Left },
            new() { FieldType = BillFieldType.Manufacturer, HeaderTitle = "Mfg", IsVisible = false, DisplayOrder = 4, WidthPercent = 8, Alignment = TextAlignmentOption.Left },
            new() { FieldType = BillFieldType.BatchNumber, HeaderTitle = "Batch", IsVisible = true, DisplayOrder = 5, WidthPercent = 9, Alignment = TextAlignmentOption.Left },
            new() { FieldType = BillFieldType.ExpiryDate, HeaderTitle = "Exp", IsVisible = true, DisplayOrder = 6, WidthPercent = 7, Alignment = TextAlignmentOption.Center },
            new() { FieldType = BillFieldType.HsnCode, HeaderTitle = "HSN", IsVisible = true, DisplayOrder = 7, WidthPercent = 7, Alignment = TextAlignmentOption.Center },
            new() { FieldType = BillFieldType.Mrp, HeaderTitle = "MRP", IsVisible = true, DisplayOrder = 8, WidthPercent = 7, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.UnitRate, HeaderTitle = "Rate", IsVisible = true, DisplayOrder = 9, WidthPercent = 7, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.Quantity, HeaderTitle = "Qty", IsVisible = true, DisplayOrder = 10, WidthPercent = 5, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.FreeQuantity, HeaderTitle = "Free", IsVisible = false, DisplayOrder = 11, WidthPercent = 5, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.DiscountPercent, HeaderTitle = "Disc%", IsVisible = true, DisplayOrder = 12, WidthPercent = 5, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.DiscountAmount, HeaderTitle = "Disc ₹", IsVisible = false, DisplayOrder = 13, WidthPercent = 6, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.GstPercent, HeaderTitle = "GST%", IsVisible = true, DisplayOrder = 14, WidthPercent = 5, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.CgstAmount, HeaderTitle = "CGST", IsVisible = false, DisplayOrder = 15, WidthPercent = 6, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.SgstAmount, HeaderTitle = "SGST", IsVisible = false, DisplayOrder = 16, WidthPercent = 6, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.IgstAmount, HeaderTitle = "IGST", IsVisible = false, DisplayOrder = 17, WidthPercent = 6, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.TaxableAmount, HeaderTitle = "Taxable", IsVisible = false, DisplayOrder = 18, WidthPercent = 8, Alignment = TextAlignmentOption.Right },
            new() { FieldType = BillFieldType.TotalAmount, HeaderTitle = "Amount (₹)", IsVisible = true, DisplayOrder = 19, WidthPercent = 10, Alignment = TextAlignmentOption.Right }
        };
    }

    public static List<BillTemplateConfig> GetAllPresets()
    {
        return new List<BillTemplateConfig>
        {
            CreateA4StandardPreset(),
            CreateA4ModernPreset(),
            CreateA4WholesalePreset(),
            CreateA5CompactPreset(),
            CreateA5RxPortraitPreset(),
            CreateThermal80mmPreset(),
            CreateThermal80mmModernPreset(),
            CreateThermal58mmPreset()
        };
    }

    public static BillTemplateConfig CreateA4StandardPreset()
    {
        return new BillTemplateConfig
        {
            Id = "preset_a4_standard",
            Name = "Standard A4 Tax Invoice (GST Compliant)",
            IsDefault = true,
            PaperSize = PaperSize.A4_Portrait,
            Styling = new BillStyleConfig
            {
                BaseFontSizePt = 9.5,
                MarginTopMm = 8.0,
                MarginBottomMm = 8.0,
                MarginLeftMm = 8.0,
                MarginRightMm = 8.0,
                PrimaryColorHex = "#1b5e20",
                ShowTableBorders = true,
                AlternateRowColors = true,
                Density = "Compact"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMACY",
                Tagline = "Complete Healthcare, Surgical & Oncology Care",
                Alignment = TextAlignmentOption.Center,
                ShowGstin = true,
                ShowDlNumbers = true,
                ShowFssai = true,
                SaleInvoiceTitle = "TAX INVOICE"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = true,
                ShowCashier = true,
                ShowCounter = true,
                ShowCustomerName = true,
                ShowCustomerPhone = true,
                ShowDoctorName = true,
                ShowDoctorRegNo = true,
                ShowPaymentMode = true
            },
            Columns = GetAllAvailableColumns(),
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = true,
                ShowHsnBreakdown = true,
                ShowCgstSgstSeparately = true
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = true,
                ShowTotalDiscount = true,
                ShowSavingsCallout = false,
                ShowRoundOff = true,
                ShowGrandTotal = true,
                ShowAmountInWords = true,
                ShowPaymentBreakdown = true,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = true,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = true,
                ShowCustomerSign = false,
                ShowAuthorizedSign = true
            }
        };
    }

    public static BillTemplateConfig CreateA4ModernPreset()
    {
        return new BillTemplateConfig
        {
            Id = "preset_a4_modern",
            Name = "Modern Clean A4 Tax Invoice",
            IsDefault = false,
            PaperSize = PaperSize.A4_Portrait,
            Styling = new BillStyleConfig
            {
                FontFamily = "'Inter', 'Segoe UI', Arial, sans-serif",
                BaseFontSizePt = 9.0,
                MarginTopMm = 6.0,
                MarginBottomMm = 6.0,
                MarginLeftMm = 6.0,
                MarginRightMm = 6.0,
                PrimaryColorHex = "#0f766e", // Teal Accent
                ShowTableBorders = false,
                AlternateRowColors = true,
                Density = "Normal"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMACY",
                Tagline = "Premium Healthcare & Wellness Solutions",
                Alignment = TextAlignmentOption.Left,
                ShowGstin = true,
                ShowDlNumbers = true,
                ShowFssai = false,
                SaleInvoiceTitle = "TAX INVOICE / CASH MEMO"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = true,
                ShowCashier = true,
                ShowCounter = false,
                ShowCustomerName = true,
                ShowCustomerPhone = true,
                ShowDoctorName = true,
                ShowDoctorRegNo = false,
                ShowPaymentMode = true
            },
            Columns = GetAllAvailableColumns(),
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = true,
                ShowHsnBreakdown = true,
                ShowCgstSgstSeparately = true
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = true,
                ShowTotalDiscount = true,
                ShowSavingsCallout = false,
                ShowRoundOff = true,
                ShowGrandTotal = true,
                ShowAmountInWords = true,
                ShowPaymentBreakdown = true,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = true,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = true,
                ShowCustomerSign = false,
                ShowAuthorizedSign = true
            }
        };
    }

    public static BillTemplateConfig CreateA4WholesalePreset()
    {
        var cols = GetAllAvailableColumns();
        foreach (var c in cols)
        {
            if (c.FieldType == BillFieldType.Packing || c.FieldType == BillFieldType.Manufacturer || c.FieldType == BillFieldType.TaxableAmount || c.FieldType == BillFieldType.CgstAmount || c.FieldType == BillFieldType.SgstAmount)
            {
                c.IsVisible = true;
            }
        }

        return new BillTemplateConfig
        {
            Id = "preset_a4_wholesale",
            Name = "Wholesale B2B A4 Tax Invoice",
            IsDefault = false,
            PaperSize = PaperSize.A4_Portrait,
            Styling = new BillStyleConfig
            {
                BaseFontSizePt = 8.5,
                MarginTopMm = 6.0,
                MarginBottomMm = 6.0,
                MarginLeftMm = 6.0,
                MarginRightMm = 6.0,
                PrimaryColorHex = "#1e3a8a", // Navy Blue
                ShowTableBorders = true,
                AlternateRowColors = true,
                Density = "Compact"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMA DISTRIBUTORS",
                Tagline = "Wholesale Pharmaceuticals & Surgical Supplies",
                Alignment = TextAlignmentOption.Center,
                ShowGstin = true,
                ShowDlNumbers = true,
                ShowFssai = true,
                SaleInvoiceTitle = "B2B TAX INVOICE"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = true,
                ShowCashier = true,
                ShowCounter = true,
                ShowCustomerName = true,
                ShowCustomerPhone = true,
                ShowCustomerAddress = true,
                ShowCustomerGstin = true,
                ShowDoctorName = false,
                ShowPaymentMode = true
            },
            Columns = cols,
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = true,
                ShowHsnBreakdown = true,
                ShowCgstSgstSeparately = true
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = true,
                ShowTotalDiscount = true,
                ShowSavingsCallout = false,
                ShowRoundOff = true,
                ShowGrandTotal = true,
                ShowAmountInWords = true,
                ShowPaymentBreakdown = true,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = true,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = false,
                ShowCustomerSign = true,
                ShowAuthorizedSign = true
            }
        };
    }

    public static BillTemplateConfig CreateA5CompactPreset()
    {
        var cols = GetAllAvailableColumns();
        foreach (var col in cols)
        {
            col.IsVisible = col.FieldType switch
            {
                BillFieldType.SrNo => true,
                BillFieldType.ItemName => true,
                BillFieldType.BatchNumber => true,
                BillFieldType.ExpiryDate => true,
                BillFieldType.Quantity => true,
                BillFieldType.Mrp => true,
                BillFieldType.UnitRate => true,
                BillFieldType.GstPercent => true,
                BillFieldType.TotalAmount => true,
                _ => false
            };
        }

        return new BillTemplateConfig
        {
            Id = "preset_a5_compact",
            Name = "Compact A5 Medical Memo (Landscape)",
            IsDefault = false,
            PaperSize = PaperSize.A5_Landscape,
            Styling = new BillStyleConfig
            {
                BaseFontSizePt = 8.5,
                MarginTopMm = 5.0,
                MarginBottomMm = 5.0,
                MarginLeftMm = 5.0,
                MarginRightMm = 5.0,
                PrimaryColorHex = "#2e7d32",
                ShowTableBorders = true,
                AlternateRowColors = true,
                Density = "Compact"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMACY",
                Tagline = "Retail Chemist & Druggist",
                Alignment = TextAlignmentOption.Center,
                ShowGstin = true,
                ShowDlNumbers = true,
                ShowFssai = false,
                SaleInvoiceTitle = "RETAIL INVOICE"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = false,
                ShowCashier = false,
                ShowCounter = true,
                ShowCustomerName = true,
                ShowCustomerPhone = true,
                ShowDoctorName = true,
                ShowDoctorRegNo = false,
                ShowPaymentMode = true
            },
            Columns = cols,
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = true,
                ShowHsnBreakdown = false,
                ShowCgstSgstSeparately = true
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = true,
                ShowTotalDiscount = true,
                ShowSavingsCallout = false,
                ShowRoundOff = true,
                ShowGrandTotal = true,
                ShowAmountInWords = false,
                ShowPaymentBreakdown = true,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = true,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = true,
                ShowCustomerSign = false,
                ShowAuthorizedSign = true
            }
        };
    }

    public static BillTemplateConfig CreateA5RxPortraitPreset()
    {
        var cols = GetAllAvailableColumns();
        foreach (var col in cols)
        {
            col.IsVisible = col.FieldType switch
            {
                BillFieldType.SrNo => true,
                BillFieldType.ItemName => true,
                BillFieldType.BatchNumber => true,
                BillFieldType.ExpiryDate => true,
                BillFieldType.Quantity => true,
                BillFieldType.UnitRate => true,
                BillFieldType.TotalAmount => true,
                _ => false
            };
        }

        return new BillTemplateConfig
        {
            Id = "preset_a5_portrait",
            Name = "A5 Prescription & Bill Combined (Portrait)",
            IsDefault = false,
            PaperSize = PaperSize.A5_Portrait,
            Styling = new BillStyleConfig
            {
                BaseFontSizePt = 8.5,
                MarginTopMm = 5.0,
                MarginBottomMm = 5.0,
                MarginLeftMm = 5.0,
                MarginRightMm = 5.0,
                PrimaryColorHex = "#15803d",
                ShowTableBorders = true,
                AlternateRowColors = false,
                Density = "Compact"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMACY",
                Tagline = "Healthcare Centre & Dispensing Chemist",
                Alignment = TextAlignmentOption.Center,
                ShowGstin = true,
                ShowDlNumbers = true,
                ShowFssai = false,
                SaleInvoiceTitle = "PRESCRIPTION BILL"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = true,
                ShowCashier = true,
                ShowCounter = false,
                ShowCustomerName = true,
                ShowCustomerPhone = true,
                ShowDoctorName = true,
                ShowDoctorRegNo = true,
                ShowPaymentMode = true
            },
            Columns = cols,
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = true,
                ShowHsnBreakdown = false
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = true,
                ShowTotalDiscount = true,
                ShowSavingsCallout = false,
                ShowRoundOff = true,
                ShowGrandTotal = true,
                ShowAmountInWords = false,
                ShowPaymentBreakdown = true,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = true,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = true,
                ShowCustomerSign = false,
                ShowAuthorizedSign = true
            }
        };
    }

    public static BillTemplateConfig CreateThermal80mmPreset()
    {
        var cols = GetAllAvailableColumns();
        foreach (var col in cols)
        {
            col.IsVisible = col.FieldType switch
            {
                BillFieldType.ItemName => true,
                BillFieldType.BatchNumber => true,
                BillFieldType.ExpiryDate => true,
                BillFieldType.Quantity => true,
                BillFieldType.UnitRate => true,
                BillFieldType.TotalAmount => true,
                _ => false
            };
        }

        return new BillTemplateConfig
        {
            Id = "preset_thermal_80mm",
            Name = "High-Speed 80mm POS Thermal Slip",
            IsDefault = false,
            PaperSize = PaperSize.Thermal_80mm,
            Styling = new BillStyleConfig
            {
                FontFamily = "monospace",
                BaseFontSizePt = 9.0,
                MarginTopMm = 2.0,
                MarginBottomMm = 2.0,
                MarginLeftMm = 2.0,
                MarginRightMm = 2.0,
                PrimaryColorHex = "#111827",
                ShowTableBorders = false,
                AlternateRowColors = false,
                Density = "Compact"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMACY",
                Tagline = "",
                Alignment = TextAlignmentOption.Center,
                ShowGstin = true,
                ShowDlNumbers = true,
                ShowFssai = false,
                SaleInvoiceTitle = "CASH MEMO"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = true,
                ShowCashier = true,
                ShowCounter = true,
                ShowCustomerName = true,
                ShowCustomerPhone = false,
                ShowDoctorName = true,
                ShowDoctorRegNo = false,
                ShowPaymentMode = true
            },
            Columns = cols,
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = false,
                ShowHsnBreakdown = false
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = true,
                ShowTotalDiscount = true,
                ShowSavingsCallout = false,
                ShowRoundOff = true,
                ShowGrandTotal = true,
                ShowAmountInWords = false,
                ShowPaymentBreakdown = true,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = true,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = false,
                ShowCustomerSign = false,
                ShowAuthorizedSign = false
            }
        };
    }

    public static BillTemplateConfig CreateThermal80mmModernPreset()
    {
        var cols = GetAllAvailableColumns();
        foreach (var col in cols)
        {
            col.IsVisible = col.FieldType switch
            {
                BillFieldType.ItemName => true,
                BillFieldType.Quantity => true,
                BillFieldType.UnitRate => true,
                BillFieldType.DiscountPercent => true,
                BillFieldType.TotalAmount => true,
                _ => false
            };
        }

        return new BillTemplateConfig
        {
            Id = "preset_thermal_80mm_modern",
            Name = "Modern 80mm Thermal Receipt (Clean)",
            IsDefault = false,
            PaperSize = PaperSize.Thermal_80mm,
            Styling = new BillStyleConfig
            {
                FontFamily = "'Segoe UI', Arial, sans-serif",
                BaseFontSizePt = 8.5,
                MarginTopMm = 2.0,
                MarginBottomMm = 2.0,
                MarginLeftMm = 2.0,
                MarginRightMm = 2.0,
                PrimaryColorHex = "#0f172a",
                ShowTableBorders = false,
                AlternateRowColors = false,
                Density = "Compact"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMACY",
                Tagline = "Quick Checkout Receipt",
                Alignment = TextAlignmentOption.Center,
                ShowGstin = true,
                ShowDlNumbers = true,
                ShowFssai = false,
                SaleInvoiceTitle = "TAX RECEIPT"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = true,
                ShowCashier = true,
                ShowCounter = false,
                ShowCustomerName = true,
                ShowDoctorName = false,
                ShowPaymentMode = true
            },
            Columns = cols,
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = true,
                ShowHsnBreakdown = false
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = true,
                ShowTotalDiscount = true,
                ShowSavingsCallout = false,
                ShowRoundOff = true,
                ShowGrandTotal = true,
                ShowAmountInWords = false,
                ShowPaymentBreakdown = true,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = false,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = false,
                ShowCustomerSign = false,
                ShowAuthorizedSign = false
            }
        };
    }

    public static BillTemplateConfig CreateThermal58mmPreset()
    {
        var cols = GetAllAvailableColumns();
        foreach (var col in cols)
        {
            col.IsVisible = col.FieldType switch
            {
                BillFieldType.ItemName => true,
                BillFieldType.Quantity => true,
                BillFieldType.TotalAmount => true,
                _ => false
            };
        }

        return new BillTemplateConfig
        {
            Id = "preset_thermal_58mm",
            Name = "Compact 58mm Thermal Slip (2-inch)",
            IsDefault = false,
            PaperSize = PaperSize.Thermal_58mm,
            Styling = new BillStyleConfig
            {
                FontFamily = "monospace",
                BaseFontSizePt = 8.0,
                MarginTopMm = 1.0,
                MarginBottomMm = 1.0,
                MarginLeftMm = 1.0,
                MarginRightMm = 1.0,
                PrimaryColorHex = "#000000",
                ShowTableBorders = false,
                AlternateRowColors = false,
                Density = "Compact"
            },
            Header = new BillHeaderConfig
            {
                IsVisible = true,
                StoreName = "MEDISTOCK PHARMACY",
                Tagline = "",
                Alignment = TextAlignmentOption.Center,
                ShowGstin = true,
                ShowDlNumbers = false,
                ShowFssai = false,
                SaleInvoiceTitle = "ESTIMATE"
            },
            Metadata = new BillMetadataConfig
            {
                ShowInvoiceNo = true,
                ShowInvoiceDate = true,
                ShowInvoiceTime = false,
                ShowCashier = false,
                ShowCounter = true,
                ShowCustomerName = false,
                ShowDoctorName = false,
                ShowPaymentMode = true
            },
            Columns = cols,
            TaxSummary = new BillTaxSummaryConfig
            {
                ShowTaxTable = false
            },
            Footer = new BillFooterConfig
            {
                ShowTotalItems = true,
                ShowTotalQuantity = true,
                ShowSubtotal = false,
                ShowTotalDiscount = false,
                ShowSavingsCallout = false,
                ShowRoundOff = false,
                ShowGrandTotal = true,
                ShowAmountInWords = false,
                ShowPaymentBreakdown = false,
                ShowBankDetails = false,
                ShowUpiQrCode = false,
                ShowTermsAndConditions = false,
                ShowGreeting = true
            },
            Signatures = new BillSignatureConfig
            {
                ShowPharmacistSign = false,
                ShowCustomerSign = false,
                ShowAuthorizedSign = false
            }
        };
    }
}

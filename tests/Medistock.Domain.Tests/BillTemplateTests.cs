using System;
using System.Linq;
using System.Text.Json;
using Medistock.Contracts.Printing;
using Xunit;

namespace Medistock.Domain.Tests;

public class BillTemplateTests
{
    [Fact]
    public void DefaultTemplate_ShouldHaveAllSectionsConfigured()
    {
        var template = new BillTemplateConfig();

        Assert.False(string.IsNullOrWhiteSpace(template.Id));
        Assert.False(string.IsNullOrWhiteSpace(template.Name));
        Assert.NotNull(template.Header);
        Assert.NotNull(template.Metadata);
        Assert.NotNull(template.Columns);
        Assert.NotNull(template.Footer);
        Assert.NotNull(template.TaxSummary);
        Assert.NotNull(template.Signatures);
        Assert.NotNull(template.Styling);
    }

    [Fact]
    public void Presets_A4Standard_ShouldHaveExpectedColumnsAndStyling()
    {
        var a4 = BillTemplatePresets.CreateA4StandardPreset();

        Assert.Contains("Standard A4 Tax Invoice", a4.Name);
        Assert.Equal(PaperSize.A4_Portrait, a4.PaperSize);
        Assert.NotEmpty(a4.Columns);
        Assert.Equal(19, a4.Columns.Count); // All 19 available columns modeled
        
        // Key columns should be visible
        Assert.True(a4.Columns.First(c => c.FieldType == BillFieldType.ItemName).IsVisible);
        Assert.True(a4.Columns.First(c => c.FieldType == BillFieldType.Quantity).IsVisible);
        Assert.True(a4.Columns.First(c => c.FieldType == BillFieldType.TotalAmount).IsVisible);
        Assert.True(a4.Header.ShowGstin);
        Assert.True(a4.Signatures.ShowPharmacistSign);
    }

    [Fact]
    public void Presets_Thermal80mm_ShouldBeConfiguredForPos()
    {
        var thermal80 = BillTemplatePresets.CreateThermal80mmPreset();

        Assert.Equal(PaperSize.Thermal_80mm, thermal80.PaperSize);
        Assert.NotEmpty(thermal80.Columns);
        Assert.False(thermal80.Signatures.ShowCustomerSign);
    }

    [Fact]
    public void Presets_Thermal58mm_ShouldBeCompact()
    {
        var thermal58 = BillTemplatePresets.CreateThermal58mmPreset();

        Assert.Equal(PaperSize.Thermal_58mm, thermal58.PaperSize);
        Assert.True(thermal58.Columns.Count(c => c.IsVisible) < thermal58.Columns.Count);
    }

    [Fact]
    public void BillTemplate_JsonSerialization_ShouldRoundtripAccurately()
    {
        var original = BillTemplatePresets.CreateA4StandardPreset();
        original.Header.StoreName = "Super Meds & Wellness";
        original.Columns.First(c => c.FieldType == BillFieldType.ItemName).HeaderTitle = "Medicine / Item";

        var json = BillTemplateJsonSerializer.Serialize(original);
        var deserialized = BillTemplateJsonSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Super Meds & Wellness", deserialized!.Header.StoreName);
        Assert.Equal("Medicine / Item", deserialized.Columns.First(c => c.FieldType == BillFieldType.ItemName).HeaderTitle);
        Assert.Equal(PaperSize.A4_Portrait, deserialized.PaperSize);
    }

    [Fact]
    public void BillTemplate_StringEnumImport_ShouldDeserializeSuccessfully()
    {
        string sampleJson = """
        {
          "id": "apex_a4_composition_bos_v2",
          "name": "APEX Pharmacy — Bill of Supply (Composition Scheme, A4)",
          "isDefault": true,
          "paperSize": "A4_Portrait",
          "header": {
            "storeName": "APEX PHARMACY",
            "alignment": "Center",
            "saleInvoiceTitle": "BILL OF SUPPLY",
            "showCompositionDeclaration": true,
            "compositionDeclarationText": "Composition Taxable Person — Not eligible to collect tax on supplies."
          },
          "columns": [
            { "fieldType": "SrNo", "headerTitle": "#", "isVisible": true, "displayOrder": 1, "alignment": "Center" },
            { "fieldType": "ItemName", "headerTitle": "Medicine", "isVisible": true, "displayOrder": 2, "alignment": "Left" }
          ]
        }
        """;

        var result = BillTemplateJsonSerializer.Deserialize(sampleJson);
        Assert.NotNull(result);
        Assert.Equal(PaperSize.A4_Portrait, result!.PaperSize);
        Assert.Equal(TextAlignmentOption.Center, result.Header.Alignment);
        Assert.Equal(BillFieldType.ItemName, result.Columns[1].FieldType);
        Assert.True(result.Header.ShowCompositionDeclaration);
    }
}

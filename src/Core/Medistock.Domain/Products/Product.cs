using System;
using System.Collections.Generic;
using Medistock.Domain.Common;

namespace Medistock.Domain.Products;

public class Product : AggregateRoot<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BrandName { get; private set; } = string.Empty;
    public string GenericName { get; private set; } = string.Empty;
    public string? ManufacturerId { get; private set; }
    public string? ManufacturerName { get; private set; }
    public string Composition { get; private set; } = string.Empty;
    public string Strength { get; private set; } = string.Empty;
    public DosageForm DosageForm { get; private set; } = DosageForm.Tablet;
    public PackSize PackSize { get; private set; } = new(10, "TAB");
    public string HsnCode { get; private set; } = string.Empty;
    public decimal GstRatePercent { get; private set; } = 12.0m;
    public DrugSchedule Schedule { get; private set; } = DrugSchedule.OTC;
    public bool IsPrescriptionRequired { get; private set; }
    public bool IsColdChain { get; private set; }
    public bool IsNarcotic { get; private set; }
    public bool IsActive { get; private set; } = true;
    public decimal MinStockAlert { get; private set; } = 10.0m;
    public string? PrimaryBarcode { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<string> _barcodes = new();
    public IReadOnlyCollection<string> Barcodes => _barcodes.AsReadOnly();

    private Product() { } // For ORM / Dapper mapping

    public static Product Create(
        string id,
        string orgId,
        string name,
        string brandName,
        string genericName,
        string composition,
        string strength,
        DosageForm dosageForm,
        PackSize packSize,
        string hsnCode,
        decimal gstRatePercent,
        DrugSchedule schedule,
        string? primaryBarcode = null,
        string? manufacturerId = null,
        string? manufacturerName = null,
        bool isColdChain = false,
        decimal minStockAlert = 10.0m)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(orgId))
            throw new ArgumentException("OrgId cannot be empty.", nameof(orgId));

        var product = new Product
        {
            Id = id,
            OrgId = orgId,
            Name = name.Trim(),
            BrandName = (brandName ?? name).Trim(),
            GenericName = (genericName ?? string.Empty).Trim(),
            Composition = (composition ?? string.Empty).Trim(),
            Strength = (strength ?? string.Empty).Trim(),
            DosageForm = dosageForm,
            PackSize = packSize ?? new PackSize(10, "TAB"),
            HsnCode = (hsnCode ?? "3004").Trim(),
            GstRatePercent = gstRatePercent,
            Schedule = schedule,
            IsPrescriptionRequired = schedule != DrugSchedule.OTC,
            IsNarcotic = schedule == DrugSchedule.ScheduleX_Narcotic,
            IsColdChain = isColdChain,
            MinStockAlert = minStockAlert > 0 ? minStockAlert : 10.0m,
            PrimaryBarcode = primaryBarcode?.Trim(),
            ManufacturerId = manufacturerId,
            ManufacturerName = manufacturerName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(primaryBarcode))
        {
            product._barcodes.Add(primaryBarcode.Trim());
        }

        return product;
    }

    public void UpdateMinStockAlert(decimal minStockAlert)
    {
        MinStockAlert = minStockAlert > 0 ? minStockAlert : 10.0m;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        var clean = barcode.Trim();
        if (!_barcodes.Contains(clean))
        {
            _barcodes.Add(clean);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void UpdatePricingAndSchedule(decimal gstRate, DrugSchedule schedule, bool isColdChain)
    {
        GstRatePercent = gstRate;
        Schedule = schedule;
        IsPrescriptionRequired = schedule != DrugSchedule.OTC;
        IsNarcotic = schedule == DrugSchedule.ScheduleX_Narcotic;
        IsColdChain = isColdChain;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}

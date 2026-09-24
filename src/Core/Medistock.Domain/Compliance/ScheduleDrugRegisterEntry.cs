using System;
using Medistock.Domain.Common;

namespace Medistock.Domain.Compliance;

public class ScheduleDrugRegisterEntry : Entity<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string BranchId { get; private set; } = string.Empty;
    public string SaleId { get; private set; } = string.Empty;
    public string InvoiceNo { get; private set; } = string.Empty;
    public DateTime SaleDate { get; private set; }
    public string ProductId { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public DrugSchedule Schedule { get; private set; }
    public string BatchNumber { get; private set; } = string.Empty;
    public DateTime ExpiryDate { get; private set; }
    public decimal Quantity { get; private set; }
    public string PatientName { get; private set; } = string.Empty;
    public string? PatientAddress { get; private set; }
    public string? PatientPhone { get; private set; }
    public string DoctorName { get; private set; } = string.Empty;
    public string? DoctorRegNo { get; private set; }
    public string? DoctorAddress { get; private set; }
    public string? PrescriptionRef { get; private set; }
    public DateTime? PrescriptionDate { get; private set; }
    public string DispensedByUserId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private ScheduleDrugRegisterEntry() { }

    public static ScheduleDrugRegisterEntry Create(
        string id,
        string orgId,
        string branchId,
        string saleId,
        string invoiceNo,
        DateTime saleDate,
        string productId,
        string productName,
        DrugSchedule schedule,
        string batchNumber,
        DateTime expiryDate,
        decimal quantity,
        string patientName,
        string? patientAddress,
        string? patientPhone,
        string doctorName,
        string? doctorRegNo,
        string? doctorAddress,
        string? prescriptionRef,
        DateTime? prescriptionDate,
        string dispensedByUserId)
    {
        if (string.IsNullOrWhiteSpace(patientName))
            throw new ArgumentException("Patient name is mandatory for Schedule Drug register.", nameof(patientName));
        if (string.IsNullOrWhiteSpace(doctorName))
            throw new ArgumentException("Doctor name is mandatory for Schedule Drug register.", nameof(doctorName));

        return new ScheduleDrugRegisterEntry
        {
            Id = id,
            OrgId = orgId,
            BranchId = branchId,
            SaleId = saleId,
            InvoiceNo = invoiceNo,
            SaleDate = saleDate,
            ProductId = productId,
            ProductName = productName,
            Schedule = schedule,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            Quantity = quantity,
            PatientName = patientName.Trim(),
            PatientAddress = patientAddress?.Trim(),
            PatientPhone = patientPhone?.Trim(),
            DoctorName = doctorName.Trim(),
            DoctorRegNo = doctorRegNo?.Trim(),
            DoctorAddress = doctorAddress?.Trim(),
            PrescriptionRef = prescriptionRef?.Trim(),
            PrescriptionDate = prescriptionDate,
            DispensedByUserId = dispensedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Medistock.Application.Compliance.Services;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Compliance;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class ScheduleDrugTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteScheduleDrugRepository _scheduleRepository;
    private readonly ScheduleDrugService _scheduleDrugService;

    public ScheduleDrugTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_sch_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);

        // Run migrations
        var migrator = new DatabaseMigrator(_connectionFactory);
        migrator.MigrateAsync().GetAwaiter().GetResult();

        _scheduleRepository = new SqliteScheduleDrugRepository(_connectionFactory);
        _scheduleDrugService = new ScheduleDrugService(_scheduleRepository);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task RecordDispenseLog_And_GetRegister_PersistsAndFiltersCorrectly()
    {
        // 1. Arrange - Prepare statutory Schedule H1 entries
        var entry1 = ScheduleDrugRegisterEntry.Create(
            id: Guid.NewGuid().ToString("N"),
            orgId: "ORG-01",
            branchId: "BR-01",
            saleId: "SALE-101",
            invoiceNo: "INV-2026-000101",
            saleDate: DateTime.UtcNow.AddHours(-2),
            productId: "PROD-AUG625",
            productName: "Augmentin 625 Duo Tablet",
            schedule: DrugSchedule.ScheduleH1,
            batchNumber: "AUG2601",
            expiryDate: new DateTime(2027, 8, 1),
            quantity: 10,
            patientName: "Vikram Malhotra",
            patientAddress: "45 Indiranagar, Bengaluru",
            patientPhone: "+91 9876543210",
            doctorName: "Dr. K. Srinivas",
            doctorRegNo: "KMC-45892",
            doctorAddress: "Apollo Clinic, Bengaluru",
            prescriptionRef: "RX-8842",
            prescriptionDate: DateTime.UtcNow.Date,
            dispensedByUserId: "PHARMACIST-01"
        );

        var entry2 = ScheduleDrugRegisterEntry.Create(
            id: Guid.NewGuid().ToString("N"),
            orgId: "ORG-01",
            branchId: "BR-01",
            saleId: "SALE-102",
            invoiceNo: "INV-2026-000102",
            saleDate: DateTime.UtcNow.AddMinutes(-30),
            productId: "PROD-ALP05",
            productName: "Alprazolam 0.5mg",
            schedule: DrugSchedule.ScheduleX_Narcotic,
            batchNumber: "ALP2504",
            expiryDate: new DateTime(2026, 12, 1),
            quantity: 5,
            patientName: "Sunita Roy",
            patientAddress: "12 Koramangala, Bengaluru",
            patientPhone: "+91 9123456780",
            doctorName: "Dr. R. Bannerjee (MD Psychiatry)",
            doctorRegNo: "KMC-11029",
            doctorAddress: "NIMHANS, Bengaluru",
            prescriptionRef: "RX-9901",
            prescriptionDate: DateTime.UtcNow.Date,
            dispensedByUserId: "PHARMACIST-01"
        );

        // 2. Act - Record log
        await _scheduleDrugService.RecordDispenseLogAsync(new List<ScheduleDrugRegisterEntry> { entry1, entry2 });

        // 3. Assert - Query all
        var allRecords = await _scheduleDrugService.GetRegisterAsync(new ScheduleDrugFilter(null, null, null, null));
        Assert.Equal(2, allRecords.Count);

        // Filter by Schedule H1 only
        var h1Records = await _scheduleDrugService.GetRegisterAsync(new ScheduleDrugFilter(null, null, DrugSchedule.ScheduleH1, null));
        Assert.Single(h1Records);
        Assert.Equal("Augmentin 625 Duo Tablet", h1Records[0].ProductName);
        Assert.Equal("Vikram Malhotra", h1Records[0].PatientName);
        Assert.Equal("Dr. K. Srinivas", h1Records[0].DoctorName);
        Assert.Equal("KMC-45892", h1Records[0].DoctorRegNo);

        // Filter by Schedule X / Narcotic only
        var xRecords = await _scheduleDrugService.GetRegisterAsync(new ScheduleDrugFilter(null, null, DrugSchedule.ScheduleX_Narcotic, null));
        Assert.Single(xRecords);
        Assert.Equal("Alprazolam 0.5mg", xRecords[0].ProductName);
        Assert.Equal("Sunita Roy", xRecords[0].PatientName);

        // Search by patient phone / doctor name
        var searchRecords = await _scheduleDrugService.GetRegisterAsync(new ScheduleDrugFilter(null, null, null, "9876543210"));
        Assert.Single(searchRecords);
        Assert.Equal("Vikram Malhotra", searchRecords[0].PatientName);
    }

    [Fact]
    public void ScheduleDrugRegisterEntry_Throws_WhenPatientOrDoctorMissing()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            ScheduleDrugRegisterEntry.Create(
                id: Guid.NewGuid().ToString("N"),
                orgId: "ORG-01",
                branchId: "BR-01",
                saleId: "SALE-01",
                invoiceNo: "INV-01",
                saleDate: DateTime.UtcNow,
                productId: "P1",
                productName: "Drug",
                schedule: DrugSchedule.ScheduleH,
                batchNumber: "B1",
                expiryDate: DateTime.UtcNow.AddYears(1),
                quantity: 1,
                patientName: "", // Invalid empty patient
                patientAddress: null,
                patientPhone: null,
                doctorName: "Dr. Valid",
                doctorRegNo: null,
                doctorAddress: null,
                prescriptionRef: null,
                prescriptionDate: null,
                dispensedByUserId: "U1"
            );
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ScheduleDrugRegisterEntry.Create(
                id: Guid.NewGuid().ToString("N"),
                orgId: "ORG-01",
                branchId: "BR-01",
                saleId: "SALE-01",
                invoiceNo: "INV-01",
                saleDate: DateTime.UtcNow,
                productId: "P1",
                productName: "Drug",
                schedule: DrugSchedule.ScheduleH,
                batchNumber: "B1",
                expiryDate: DateTime.UtcNow.AddYears(1),
                quantity: 1,
                patientName: "Valid Patient",
                patientAddress: null,
                patientPhone: null,
                doctorName: "", // Invalid empty doctor
                doctorRegNo: null,
                doctorAddress: null,
                prescriptionRef: null,
                prescriptionDate: null,
                dispensedByUserId: "U1"
            );
        });
    }
}

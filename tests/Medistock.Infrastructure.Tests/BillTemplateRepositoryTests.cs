using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Printing;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class BillTemplateRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly SqliteBillTemplateRepository _repo;

    public BillTemplateRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_test_bill_repo_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _repo = new SqliteBillTemplateRepository(_connectionFactory);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { }
    }

    [Fact]
    public async Task MigrationAndDefaultTemplate_ShouldSeedAndReturnA4Default()
    {
        await _migrator.MigrateAsync();

        var defaultTemplate = await _repo.GetDefaultTemplateAsync();

        Assert.NotNull(defaultTemplate);
        Assert.True(defaultTemplate.IsDefault);
        Assert.Contains("Standard A4 Tax Invoice", defaultTemplate.Name);
        Assert.Equal(PaperSize.A4_Portrait, defaultTemplate.PaperSize);
        Assert.Equal(19, defaultTemplate.Columns.Count);
    }

    [Fact]
    public async Task ListAllTemplates_ShouldReturnFourPresets()
    {
        await _migrator.MigrateAsync();

        var templates = await _repo.ListAllTemplatesAsync();

        Assert.True(templates.Count >= 4);
        Assert.Contains(templates, t => t.PaperSize == PaperSize.A4_Portrait);
        Assert.Contains(templates, t => t.PaperSize == PaperSize.A5_Landscape);
        Assert.Contains(templates, t => t.PaperSize == PaperSize.Thermal_80mm);
        Assert.Contains(templates, t => t.PaperSize == PaperSize.Thermal_58mm);
    }

    [Fact]
    public async Task SaveTemplate_AndSetDefault_ShouldPersistModifications()
    {
        await _migrator.MigrateAsync();

        var custom = BillTemplatePresets.CreateThermal80mmPreset();
        custom.Id = "custom_80mm_counter1";
        custom.Name = "Counter 1 Express Slip";
        custom.IsDefault = false;
        custom.Header.StoreName = "Apex Pharmacy Branch 2";

        await _repo.SaveTemplateAsync(custom);

        var retrieved = await _repo.GetTemplateByIdAsync("custom_80mm_counter1");
        Assert.NotNull(retrieved);
        Assert.Equal("Apex Pharmacy Branch 2", retrieved!.Header.StoreName);

        // Set as default
        await _repo.SetDefaultTemplateAsync("custom_80mm_counter1");
        var newDefault = await _repo.GetDefaultTemplateAsync();
        Assert.Equal("custom_80mm_counter1", newDefault.Id);
    }

    [Fact]
    public async Task PrinterConfigurations_SaveAndRetrieve_ShouldWork()
    {
        await _migrator.MigrateAsync();

        var printer = new PrinterConfigurationDto
        {
            Id = "p_pos_1",
            Name = "EPSON TM-T82 Receipt",
            InterfaceType = PrinterInterfaceType.WindowsSpoolerRaw,
            TargetNameOrIp = "EPSON TM-T82 Receipt",
            PaperSize = PaperSize.Thermal_80mm,
            AssignedTemplateId = "preset_thermal_80mm",
            AutoCutPaper = true,
            KickCashDrawer = true,
            IsDefaultPos = true,
            IsDefaultA4 = false
        };

        await _repo.SavePrinterConfigAsync(printer);

        var posDefault = await _repo.GetDefaultPosPrinterAsync();
        Assert.NotNull(posDefault);
        Assert.Equal("EPSON TM-T82 Receipt", posDefault!.Name);
        Assert.Equal(PrinterInterfaceType.WindowsSpoolerRaw, posDefault.InterfaceType);
    }
}

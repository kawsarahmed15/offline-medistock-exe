using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Medistock.Infrastructure.Data.Repositories;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class ProductSearchAndSeedingTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseMigrator _migrator;
    private readonly DataSeeder _seeder;
    private readonly SqliteProductSearchRepository _searchRepo;

    public ProductSearchAndSeedingTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"medistock_seeder_test_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory(_dbPath);
        _migrator = new DatabaseMigrator(_connectionFactory);
        _seeder = new DataSeeder(_connectionFactory);
        _searchRepo = new SqliteProductSearchRepository(_connectionFactory);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
            var wal = _dbPath + "-wal";
            var shm = _dbPath + "-shm";
            if (File.Exists(wal)) File.Delete(wal);
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }

    [Fact]
    public async Task DataSeeder_DefaultInstall_DoesNotAutoSeedProducts()
    {
        await _migrator.MigrateAsync();
        await _seeder.SeedIfEmptyAsync();

        using var conn = await _connectionFactory.CreateConnectionAsync();
        var productCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM products;");
        var customerCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM customers;");
        var supplierCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM suppliers;");

        // Verify product catalog is kept clean on install
        Assert.Equal(0, productCount);
        Assert.True(customerCount > 0);
        Assert.True(supplierCount > 0);
    }

    [Fact]
    public async Task DataSeeder_SeedSampleProducts_SeedsMoreThan200MedicinesWithBatchesAndStock()
    {
        await _migrator.MigrateAsync();
        await _seeder.SeedIfEmptyAsync();
        await _seeder.SeedSampleProductsAsync();

        using var conn = await _connectionFactory.CreateConnectionAsync();
        var productCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM products;");
        var batchCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM batches;");
        var stockCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM stock_balances;");

        Assert.True(productCount >= 200, $"Expected >= 200 products, got {productCount}");
        Assert.True(batchCount >= 200, $"Expected >= 200 batches, got {batchCount}");
        Assert.True(stockCount >= 200, $"Expected >= 200 stock balance entries, got {stockCount}");
    }

    [Theory]
    [InlineData("d", "Dolo 650mg Tablet")]
    [InlineData("p", "Pan-D Capsule")]
    [InlineData("a", "Augmentin 625 Duo Tablet")]
    [InlineData("c", "Calpol 650mg Tablet")]
    [InlineData("m", "Montair-LC Tablet")]
    [InlineData("t", "Telma 40mg Tablet")]
    public async Task SingleCharacterSearch_ReturnsMatchingMedicinesInstantly(string singleCharQuery, string expectedSampleMatch)
    {
        await _migrator.MigrateAsync();
        await _seeder.SeedIfEmptyAsync();
        await _seeder.SeedSampleProductsAsync();

        var results = await _searchRepo.SearchProductsAsync(singleCharQuery, "wh-1", 20);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name.Contains(expectedSampleMatch, StringComparison.OrdinalIgnoreCase)
                                   || r.BrandName.Contains(singleCharQuery, StringComparison.OrdinalIgnoreCase)
                                   || r.GenericName.Contains(singleCharQuery, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateProductWithBatch_CreatesAndRetrievesFefoBatchesCorrectly()
    {
        await _migrator.MigrateAsync();

        var productRepo = new SqliteProductRepository(_connectionFactory);

        var pId = "prod_custom_1";
        var bId = "batch_custom_1";

        var product = Medistock.Domain.Products.Product.Create(
            pId, "org-1", "Azithral 500mg Tab", "Azithral", "Azithromycin 500mg",
            "Azithromycin 500mg", "500mg", Medistock.Domain.Common.DosageForm.Tablet,
            new Medistock.Domain.Common.PackSize(5, "TAB"), "3004", 12.0m,
            Medistock.Domain.Common.DrugSchedule.ScheduleH,
            primaryBarcode: "8901234567890",
            manufacturerName: "Alembic Pharma"
        );

        var batch = Medistock.Domain.Inventory.Batch.Create(
            bId, pId, "org-1", "AZ500-24", DateTime.UtcNow.AddMonths(18),
            120.0m, 80.0m, 108.0m
        );

        await productRepo.CreateProductWithBatchAsync(product, batch, 50, "wh-1");

        var searchResults = await _searchRepo.SearchProductsAsync("Azithral", "wh-1");
        Assert.Single(searchResults);
        var found = searchResults[0];
        Assert.Equal("Azithral 500mg Tab", found.Name);
        Assert.Equal("Alembic Pharma", found.ManufacturerName);
        Assert.NotEmpty(found.Batches);
        Assert.Equal("AZ500-24", found.Batches[0].BatchNumber);
        Assert.Equal(50, found.Batches[0].AvailableQuantity);

        var batches = await _searchRepo.GetBatchesForProductAsync(pId, "wh-1");
        Assert.Single(batches);
        Assert.Equal("AZ500-24", batches[0].BatchNumber);
        Assert.Equal(120.0m, batches[0].Mrp);
    }

    [Fact]
    public async Task MedicineCatalogSeeder_SeedFromJson_And_Clear_WorksCorrectly()
    {
        await _migrator.MigrateAsync();
        var catalogSeeder = new MedicineCatalogSeeder(_connectionFactory);

        var tempJsonPath = Path.Combine(Path.GetTempPath(), $"test_meds_{Guid.NewGuid():N}.json");
        var sampleJson = """
        [
          {
            "id": "1",
            "name": "Augmentin 625 Duo Tablet",
            "price(₹)": "223.42",
            "Is_discontinued": "FALSE",
            "manufacturer_name": "Glaxo SmithKline Pharmaceuticals Ltd",
            "type": "allopathy",
            "pack_size_label": "strip of 10 tablets",
            "short_composition1": "Amoxycillin  (500mg) ",
            "short_composition2": "  Clavulanic Acid (125mg)"
          },
          {
            "id": "2",
            "name": "Azithral 500 Tablet",
            "price(₹)": "132.36",
            "Is_discontinued": "FALSE",
            "manufacturer_name": "Alembic Pharmaceuticals Ltd",
            "type": "allopathy",
            "pack_size_label": "strip of 5 tablets",
            "short_composition1": "Azithromycin (500mg)",
            "short_composition2": ""
          }
        ]
        """;

        await File.WriteAllTextAsync(tempJsonPath, sampleJson);

        try
        {
            var initialCount = await catalogSeeder.GetProductCountAsync();
            Assert.Equal(0, initialCount);

            var seeded = await catalogSeeder.SeedFromJsonFileAsync(tempJsonPath);
            Assert.Equal(2, seeded);

            var afterCount = await catalogSeeder.GetProductCountAsync();
            Assert.Equal(2, afterCount);

            var searchRes = await _searchRepo.SearchProductsAsync("Augmentin", "wh-1");
            Assert.Single(searchRes);
            Assert.Equal("Augmentin 625 Duo Tablet", searchRes[0].Name);

            var cleared = await catalogSeeder.ClearAllProductsAsync();
            Assert.Equal(2, cleared);

            var finalCount = await catalogSeeder.GetProductCountAsync();
            Assert.Equal(0, finalCount);
        }
        finally
        {
            if (File.Exists(tempJsonPath)) File.Delete(tempJsonPath);
        }
    }
}

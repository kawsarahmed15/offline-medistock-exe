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
    public async Task DataSeeder_SeedsMoreThan200MedicinesWithBatchesAndStock()
    {
        await _migrator.MigrateAsync();
        await _seeder.SeedIfEmptyAsync();

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

        var results = await _searchRepo.SearchProductsAsync(singleCharQuery, "wh-1", 20);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name.Contains(expectedSampleMatch, StringComparison.OrdinalIgnoreCase)
                                   || r.BrandName.Contains(singleCharQuery, StringComparison.OrdinalIgnoreCase)
                                   || r.GenericName.Contains(singleCharQuery, StringComparison.OrdinalIgnoreCase));
    }
}

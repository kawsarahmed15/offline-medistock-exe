using System;
using System.IO;
using System.Threading.Tasks;
using Medistock.Infrastructure.Data.Migrations;
using Medistock.Infrastructure.Data.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class DatabaseRepairTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dbPath;

    public DatabaseRepairTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MedistockRepairTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void EnsureDatabaseHealthy_WhenFileDoesNotExist_ReturnsTrue()
    {
        var result = DatabaseRepairService.EnsureDatabaseHealthy(_dbPath);
        Assert.True(result);
    }

    [Fact]
    public async Task EnsureDatabaseHealthy_WhenDatabaseIsMalformed_AutoHealsAndEnablesMigration()
    {
        // 1. Create a corrupt file with invalid header bytes
        File.WriteAllBytes(_dbPath, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 });

        // 2. EnsureDatabaseHealthy should detect corrupt header and auto-heal
        var healthy = DatabaseRepairService.EnsureDatabaseHealthy(_dbPath);
        Assert.True(healthy);

        // 3. Verify that the file is now a valid SQLite database by executing migrations
        var factory = new SqliteConnectionFactory(_dbPath);
        var migrator = new DatabaseMigrator(factory);
        await migrator.MigrateAsync();

        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM __schema_migrations;";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.True(count > 0);
    }
}

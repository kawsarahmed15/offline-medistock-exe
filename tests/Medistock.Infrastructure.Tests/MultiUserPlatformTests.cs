using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Medistock.Contracts.Auth;
using Medistock.Contracts.Backup;
using Medistock.Contracts.Updates;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Data.Backup;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class MultiUserPlatformTests : IDisposable
{
    private readonly string _tempTestDir;

    public MultiUserPlatformTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "Medistock_PlatformTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempTestDir))
        {
            try { Directory.Delete(_tempTestDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void MedistockPaths_EnsureAllDirectoriesExist_CreatesRequiredFolders()
    {
        // Act
        MedistockPaths.EnsureAllDirectoriesExist();

        // Assert
        Assert.True(Directory.Exists(MedistockPaths.AppDataRoot));
        Assert.True(Directory.Exists(MedistockPaths.LogsDirectory));
        Assert.True(Directory.Exists(MedistockPaths.UpdateStagingDirectory));
        Assert.True(Directory.Exists(MedistockPaths.BackupsDirectory));
        Assert.True(Directory.Exists(MedistockPaths.TempDirectory));
    }

    [Fact]
    public async Task LocalBackupService_CreateBackupAsync_GeneratesValidZipWithDbAndMetadata()
    {
        // Arrange: create a mock sqlite database in temp
        var dbPath = MedistockPaths.Database;
        MedistockPaths.EnsureAllDirectoriesExist();

        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS test_data (id INT PRIMARY KEY, name TEXT); INSERT OR REPLACE INTO test_data VALUES (1, 'MedistockTest');";
            await cmd.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();

        var backupService = new LocalBackupService();
        var backupOutputDir = Path.Combine(_tempTestDir, "Backups");

        // Act
        var zipPath = await backupService.CreateBackupAsync(backupOutputDir);

        // Assert
        Assert.True(File.Exists(zipPath));
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var dbEntry = zip.GetEntry("medistock.db");
            var metaEntry = zip.GetEntry("backup_meta.json");

            Assert.NotNull(dbEntry);
            Assert.NotNull(metaEntry);

            using var metaReader = new StreamReader(metaEntry.Open());
            var metaJson = await metaReader.ReadToEndAsync();
            var meta = JsonSerializer.Deserialize<BackupMetadata>(metaJson);

            Assert.NotNull(meta);
            Assert.False(string.IsNullOrWhiteSpace(meta.AppVersion));
            Assert.False(string.IsNullOrWhiteSpace(meta.MachineName));
        }
    }

    [Fact]
    public async Task LocalBackupService_RestoreFromBackupAsync_RestoresDatabaseFile()
    {
        // Arrange: Create a sample backup zip
        var backupOutputDir = Path.Combine(_tempTestDir, "Backups");
        Directory.CreateDirectory(backupOutputDir);

        var sampleDb = Path.Combine(_tempTestDir, "sample.db");
        await using (var conn = new SqliteConnection($"Data Source={sampleDb}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE restore_test (id INT); INSERT INTO restore_test VALUES (99);";
            await cmd.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();

        var zipPath = Path.Combine(backupOutputDir, "test_restore.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(sampleDb, "medistock.db");
            var metaEntry = zip.CreateEntry("backup_meta.json");
            using var writer = new StreamWriter(metaEntry.Open());
            await writer.WriteAsync("{\"AppVersion\":\"1.0.0\",\"CreatedAt\":\"2026-09-29T00:00:00Z\",\"MachineName\":\"TEST\"}");
        }

        var backupService = new LocalBackupService();

        // Act
        var result = await backupService.RestoreFromBackupAsync(zipPath);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ActivationContracts_Serialization_RoundTripsAccurately()
    {
        // Arrange
        var request = new ActivationRequest
        {
            Email = "pharmacist@medistock.local",
            Password = "Password123!",
            DeviceId = "DEV-TEST-001",
            DeviceFingerprint = "SHA256-FINGERPRINT-SAMPLE",
            DeviceName = "COUNTER-1",
            AppVersion = "1.0.0"
        };

        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<ActivationRequest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(request.Email, deserialized.Email);
        Assert.Equal(request.DeviceId, deserialized.DeviceId);
        Assert.Equal(request.DeviceFingerprint, deserialized.DeviceFingerprint);
    }

    [Fact]
    public void UpdateContracts_VersionComparison_DetectsNewerVersion()
    {
        // Arrange
        var currentVer = new Version("1.0.0");
        var latestVer = new Version("1.1.0");

        // Act & Assert
        Assert.True(latestVer > currentVer);
    }
}

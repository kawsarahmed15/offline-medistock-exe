using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Data.Backup;

public class LocalBackupService : ILocalBackupService
{
    private readonly ILogger<LocalBackupService>? _logger;

    public LocalBackupService(ILogger<LocalBackupService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<string> CreateBackupAsync(string? destinationFolder = null, CancellationToken ct = default)
    {
        var dest = destinationFolder ?? MedistockPaths.BackupsDirectory;
        Directory.CreateDirectory(dest);
        MedistockPaths.EnsureAllDirectoriesExist();

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var zipPath = Path.Combine(dest, $"Medistock_Backup_{timestamp}.zip");
        var tempDbCopy = Path.Combine(MedistockPaths.TempDirectory, $"backup_{Guid.NewGuid():N}.db");

        try
        {
            // SQLite Online / Hot Backup API (safe even while app is reading/writing)
            var srcConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = MedistockPaths.Database,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            var destConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = tempDbCopy,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            await using (var srcConn = new SqliteConnection(srcConnStr))
            await using (var destConn = new SqliteConnection(destConnStr))
            {
                await srcConn.OpenAsync(ct);
                await destConn.OpenAsync(ct);
                srcConn.BackupDatabase(destConn);
            }

            // Explicitly release file locks on Windows
            SqliteConnection.ClearAllPools();

            // Write metadata
            var meta = new BackupMetadata
            {
                AppVersion = typeof(LocalBackupService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
                CreatedAt = DateTime.UtcNow.ToString("O"),
                MachineName = Environment.MachineName
            };
            var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });

            // Create ZIP archive
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(tempDbCopy, "medistock.db");

                var metaEntry = zip.CreateEntry("backup_meta.json");
                await using var metaStream = metaEntry.Open();
                await using var writer = new StreamWriter(metaStream);
                await writer.WriteAsync(metaJson);
            }

            _logger?.LogInformation("Local backup created successfully at {Path}", zipPath);
            return zipPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create local backup");
            throw;
        }
        finally
        {
            if (File.Exists(tempDbCopy))
            {
                try { File.Delete(tempDbCopy); } catch { }
            }
        }
    }

    public async Task<RestoreResult> RestoreFromBackupAsync(string zipPath, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(zipPath))
                return new RestoreResult(false, "Backup file not found.");

            var tempExtract = Path.Combine(MedistockPaths.TempDirectory, "restore_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempExtract);

            try
            {
                // Validate ZIP structure
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var dbEntry = zip.GetEntry("medistock.db");
                    if (dbEntry == null)
                        return new RestoreResult(false, "Invalid backup archive: missing medistock.db");
                }

                ZipFile.ExtractToDirectory(zipPath, tempExtract, overwriteFiles: true);
                var extractedDb = Path.Combine(tempExtract, "medistock.db");

                if (!File.Exists(extractedDb))
                    return new RestoreResult(false, "Extracted backup database not found.");

                // Clear connection pools so the file lock is released
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Replace current DB file
                File.Copy(extractedDb, MedistockPaths.Database, overwrite: true);

                _logger?.LogInformation("Database restored successfully from {Path}", zipPath);
                return new RestoreResult(true, null);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to restore database from backup");
                return new RestoreResult(false, ex.Message);
            }
            finally
            {
                if (Directory.Exists(tempExtract))
                {
                    try { Directory.Delete(tempExtract, recursive: true); } catch { }
                }
            }
        }, ct);
    }

    public IReadOnlyList<BackupFileInfo> ListLocalBackups(string? folder = null)
    {
        var targetDir = !string.IsNullOrWhiteSpace(folder) ? folder : MedistockPaths.BackupsDirectory;
        if (!Directory.Exists(targetDir))
            return Array.Empty<BackupFileInfo>();

        return Directory.GetFiles(targetDir, "Medistock_Backup_*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupFileInfo(f.Name, f.FullName, f.CreationTime, f.Length))
            .ToList();
    }

    public int PruneOldBackups(string? folder = null, int retentionDays = 7)
    {
        var targetDir = !string.IsNullOrWhiteSpace(folder) ? folder : MedistockPaths.BackupsDirectory;
        if (!Directory.Exists(targetDir)) return 0;

        var cutoff = DateTime.Now.AddDays(-retentionDays);
        int deletedCount = 0;

        foreach (var file in Directory.GetFiles(targetDir, "Medistock_Backup_*.zip"))
        {
            try
            {
                var fi = new FileInfo(file);
                if (fi.CreationTime < cutoff && fi.LastWriteTime < cutoff)
                {
                    fi.Delete();
                    deletedCount++;
                    _logger?.LogInformation("Pruned old backup archive: {FileName}", fi.Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete old backup file: {File}", file);
            }
        }

        return deletedCount;
    }

    public async Task<string?> CreateDailyBackupIfDueAsync(string? destinationFolder = null, CancellationToken ct = default)
    {
        var targetDir = !string.IsNullOrWhiteSpace(destinationFolder) ? destinationFolder : MedistockPaths.BackupsDirectory;
        if (Directory.Exists(targetDir))
        {
            var today = DateTime.Today;
            var existingBackups = Directory.GetFiles(targetDir, "Medistock_Backup_*.zip")
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTime.Date == today || f.LastWriteTime.Date == today)
                .ToList();

            if (existingBackups.Count > 0)
            {
                _logger?.LogDebug("Daily backup already exists for today ({Count} backup(s) found)", existingBackups.Count);
                return null;
            }
        }

        _logger?.LogInformation("No local backup created today. Creating automatic daily backup...");
        return await CreateBackupAsync(targetDir, ct);
    }
}

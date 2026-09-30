using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Medistock.Infrastructure.Data.Persistence;

public static class DatabaseRepairService
{
    public static bool EnsureDatabaseHealthy(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            return true;
        }

        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";
            var result = cmd.ExecuteScalar()?.ToString();
            if (string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Quick check returned something other than ok
            return AutoHealCorruptDatabase(dbPath);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 11 || (ex.Message != null && ex.Message.IndexOf("malformed", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return AutoHealCorruptDatabase(dbPath);
        }
        catch (Exception)
        {
            // If the database cannot even be opened due to file header corruption
            return AutoHealCorruptDatabase(dbPath);
        }
    }

    public static bool AutoHealCorruptDatabase(string dbPath)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var dataDir = Path.GetDirectoryName(dbPath) ?? "";
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var corruptBackupDir = Path.Combine(dataDir, $"corrupt_backup_{ts}");
            Directory.CreateDirectory(corruptBackupDir);

            var files = new[]
            {
                dbPath,
                dbPath + "-wal",
                dbPath + "-shm"
            };

            foreach (var f in files)
            {
                if (File.Exists(f))
                {
                    try
                    {
                        var dest = Path.Combine(corruptBackupDir, Path.GetFileName(f));
                        File.Copy(f, dest, true);
                        File.Delete(f);
                    }
                    catch { }
                }
            }

            // Create a pristine new database file
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            cmd.ExecuteNonQuery();

            return true;
        }
        catch
        {
            return false;
        }
    }
}

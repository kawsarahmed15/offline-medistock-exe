using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Backup;
using Medistock.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync.Backup;

public class CloudBackupService : ICloudBackupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudBackupService>? _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Standard salt for local device backup key derivation
    private static readonly byte[] EncryptionKeySalt = Encoding.UTF8.GetBytes("Medistock_Cloud_Backup_Salt_v1");

    public CloudBackupService(HttpClient httpClient, ILogger<CloudBackupService>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task<CloudBackupResult> UploadBackupAsync(CancellationToken ct = default)
    {
        MedistockPaths.EnsureAllDirectoriesExist();

        var tempSnapshot = Path.Combine(MedistockPaths.TempDirectory, $"cloud_snap_{Guid.NewGuid():N}.db");
        var tempEncrypted = Path.Combine(MedistockPaths.TempDirectory, $"cloud_snap_{Guid.NewGuid():N}.enc");

        try
        {
            // 1. Create hot snapshot of SQLite DB
            var srcConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = MedistockPaths.Database,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            var destConnStr = new SqliteConnectionStringBuilder
            {
                DataSource = tempSnapshot,
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

            // 2. Encrypt with AES-256
            byte[] rawBytes;
            using (var fs = new FileStream(tempSnapshot, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                rawBytes = new byte[fs.Length];
                await fs.ReadExactlyAsync(rawBytes, ct);
            }
            var encryptedBytes = EncryptAes(rawBytes, GetLocalEncryptionKey());
            await File.WriteAllBytesAsync(tempEncrypted, encryptedBytes, ct);

            // 3. Upload to CloudApi
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(encryptedBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "backupFile", "backup.enc");

            var orgId = "ORG-001";
            var deviceId = Environment.MachineName;

            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/backups/upload");
            req.Headers.Add("X-Org-Id", orgId);
            req.Headers.Add("X-Device-Id", deviceId);
            req.Headers.Add("X-App-Version", typeof(CloudBackupService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
            req.Content = form;

            using var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogWarning("Cloud backup upload failed: {StatusCode} {Error}", response.StatusCode, err);
                return new CloudBackupResult(false, null, $"Server returned {response.StatusCode}: {err}");
            }

            var result = await response.Content.ReadFromJsonAsync<CloudBackupUploadResponse>(JsonOpts, ct);
            _logger?.LogInformation("Cloud backup upload completed successfully. BackupId: {Id}", result?.BackupId);
            return new CloudBackupResult(true, result?.BackupId, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to upload cloud backup");
            return new CloudBackupResult(false, null, ex.Message);
        }
        finally
        {
            if (File.Exists(tempSnapshot)) try { File.Delete(tempSnapshot); } catch { }
            if (File.Exists(tempEncrypted)) try { File.Delete(tempEncrypted); } catch { }
        }
    }

    public async Task UploadBackupIfDueAsync(CancellationToken ct = default)
    {
        try
        {
            // Simple check: don't backup more often than once per 12 hours
            var markerFile = Path.Combine(MedistockPaths.AppDataRoot, "Config", "last_cloud_backup.txt");
            if (File.Exists(markerFile))
            {
                var text = await File.ReadAllTextAsync(markerFile, ct);
                if (DateTime.TryParse(text, out var last) && DateTime.UtcNow - last < TimeSpan.FromHours(12))
                {
                    _logger?.LogDebug("Skipping cloud backup: last backup was at {Last}", last);
                    return;
                }
            }

            var result = await UploadBackupAsync(ct);
            if (result.Success)
            {
                await File.WriteAllTextAsync(markerFile, DateTime.UtcNow.ToString("O"), ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Automatic cloud backup check encountered an error");
        }
    }

    public async Task<IReadOnlyList<CloudBackupInfo>> ListCloudBackupsAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/backups");
            req.Headers.Add("X-Org-Id", "ORG-001");

            using var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<CloudBackupInfo>();

            var list = await response.Content.ReadFromJsonAsync<List<CloudBackupInfo>>(JsonOpts, ct);
            return list ?? (IReadOnlyList<CloudBackupInfo>)Array.Empty<CloudBackupInfo>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list cloud backups");
            return Array.Empty<CloudBackupInfo>();
        }
    }

    public async Task<string> DownloadAndDecryptBackupAsync(string backupId, string destinationPath, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/backups/{backupId}/download");
        req.Headers.Add("X-Org-Id", "ORG-001");

        using var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var encryptedBytes = await response.Content.ReadAsByteArrayAsync(ct);
        var decryptedBytes = DecryptAes(encryptedBytes, GetLocalEncryptionKey());

        await File.WriteAllBytesAsync(destinationPath, decryptedBytes, ct);
        return destinationPath;
    }

    public async Task<Medistock.Infrastructure.Data.Backup.RestoreResult> RestoreCloudBackupAsync(string backupId, CancellationToken ct = default)
    {
        MedistockPaths.EnsureAllDirectoriesExist();
        var tempDecrypted = Path.Combine(MedistockPaths.TempDirectory, $"cloud_restore_{Guid.NewGuid():N}.db");

        try
        {
            await DownloadAndDecryptBackupAsync(backupId, tempDecrypted, ct);

            if (!File.Exists(tempDecrypted) || new FileInfo(tempDecrypted).Length == 0)
            {
                return new Medistock.Infrastructure.Data.Backup.RestoreResult(false, "Decrypted backup database is empty or corrupt.");
            }

            // Clear connection pools so file locks are released
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Replace current local database
            File.Copy(tempDecrypted, MedistockPaths.Database, overwrite: true);

            _logger?.LogInformation("Database restored successfully from cloud backup {Id}", backupId);
            return new Medistock.Infrastructure.Data.Backup.RestoreResult(true, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore database from cloud backup {Id}", backupId);
            return new Medistock.Infrastructure.Data.Backup.RestoreResult(false, ex.Message);
        }
        finally
        {
            if (File.Exists(tempDecrypted))
            {
                try { File.Delete(tempDecrypted); } catch { }
            }
        }
    }

    private static byte[] GetLocalEncryptionKey()
    {
        // Derive consistent 256-bit key from local machine identifier + salt
        using var kdf = new Rfc2898DeriveBytes(
            Environment.MachineName + Environment.ProcessorCount,
            EncryptionKeySalt,
            10000,
            HashAlgorithmName.SHA256);
        return kdf.GetBytes(32); // 256-bit key
    }

    private static byte[] EncryptAes(byte[] plainBytes, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // prepend IV

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    private static byte[] DecryptAes(byte[] encryptedBytes, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;

        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
        {
            cs.Write(encryptedBytes, iv.Length, encryptedBytes.Length - iv.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }
}

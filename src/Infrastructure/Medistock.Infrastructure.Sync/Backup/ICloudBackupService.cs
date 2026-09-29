using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Backup;

using Medistock.Infrastructure.Data.Backup;

namespace Medistock.Infrastructure.Sync.Backup;

public record CloudBackupResult(bool Success, string? BackupId, string? ErrorMessage);

public interface ICloudBackupService
{
    Task<CloudBackupResult> UploadBackupAsync(CancellationToken ct = default);
    Task UploadBackupIfDueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CloudBackupInfo>> ListCloudBackupsAsync(CancellationToken ct = default);
    Task<string> DownloadAndDecryptBackupAsync(string backupId, string destinationPath, CancellationToken ct = default);
    Task<RestoreResult> RestoreCloudBackupAsync(string backupId, CancellationToken ct = default);
}

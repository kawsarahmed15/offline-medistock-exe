using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Medistock.Infrastructure.Data.Backup;

public interface ILocalBackupService
{
    Task<string> CreateBackupAsync(string? destinationFolder = null, CancellationToken ct = default);
    Task<string?> CreateDailyBackupIfDueAsync(string? destinationFolder = null, CancellationToken ct = default);
    Task<RestoreResult> RestoreFromBackupAsync(string zipPath, CancellationToken ct = default);
    IReadOnlyList<BackupFileInfo> ListLocalBackups(string? folder = null);
    int PruneOldBackups(string? folder = null, int maxBackupsToKeep = 7);
}

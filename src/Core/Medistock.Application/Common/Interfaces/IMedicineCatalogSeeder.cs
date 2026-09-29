using System;
using System.Threading;
using System.Threading.Tasks;

namespace Medistock.Application.Common.Interfaces;

public record MedicineSeedProgress(int Processed, int Total, string Message, double Percentage);

public interface IMedicineCatalogSeeder
{
    /// <summary>
    /// Returns the total number of products currently in the database.
    /// </summary>
    Task<int> GetProductCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds the full master medicine catalog from a JSON file with streaming / batching and live progress updates.
    /// </summary>
    Task<int> SeedFromJsonFileAsync(
        string jsonFilePath,
        IProgress<MedicineSeedProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds a starter pack of ~220 common Indian medicines with sample batches and opening stock.
    /// </summary>
    Task<int> SeedSampleStarterPackAsync(
        IProgress<MedicineSeedProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all products, batches, stock balances, and barcodes from the catalog.
    /// </summary>
    Task<int> ClearAllProductsAsync(CancellationToken cancellationToken = default);
}

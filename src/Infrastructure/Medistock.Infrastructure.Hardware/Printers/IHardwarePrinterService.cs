using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;

namespace Medistock.Infrastructure.Hardware.Printers;

public record PrinterDeviceInfo(
    string Name,
    bool IsDefault,
    bool IsNetwork,
    string Status
);

public interface IHardwarePrinterService
{
    Task<List<PrinterDeviceInfo>> GetInstalledPrintersAsync();
    Task<bool> PrintRawEscPosAsync(string printerName, byte[] data, string docTitle = "Medistock Receipt", CancellationToken cancellationToken = default);
    Task<bool> PrintNetworkTcpAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default);
    Task<bool> PrintReceiptAsync(SaleReceiptModel receipt, BillTemplateConfig config, string? targetPrinter = null, CancellationToken cancellationToken = default);
}

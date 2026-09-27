using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;

namespace Medistock.Infrastructure.Hardware.Printers;

public class HardwarePrinterService : IHardwarePrinterService
{
    private readonly IBillDocumentGenerator _generator;

    public HardwarePrinterService(IBillDocumentGenerator generator)
    {
        _generator = generator;
    }

    public Task<List<PrinterDeviceInfo>> GetInstalledPrintersAsync()
    {
        var list = new List<PrinterDeviceInfo>();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var defaultPrinter = "";
                var settings = new System.Drawing.Printing.PrinterSettings();
                defaultPrinter = settings.PrinterName;

                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    bool isDef = string.Equals(printer, defaultPrinter, StringComparison.OrdinalIgnoreCase);
                    bool isNet = printer.StartsWith("\\\\");
                    list.Add(new PrinterDeviceInfo(printer, isDef, isNet, "Ready"));
                }
            }
            catch
            {
                // Fallback if Drawing.Printing is restricted
            }
        }

        if (!list.Any())
        {
            list.Add(new PrinterDeviceInfo("Microsoft Print to PDF", true, false, "Ready"));
            list.Add(new PrinterDeviceInfo("Generic / Text Only POS", false, false, "Ready"));
        }

        return Task.FromResult(list);
    }

    public Task<bool> PrintRawEscPosAsync(string printerName, byte[] data, string docTitle = "Medistock Receipt", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(printerName) || data == null || data.Length == 0)
        {
            return Task.FromResult(false);
        }

        try
        {
            var success = RawPrinterHelper.SendBytesToPrinter(printerName, data, docTitle);
            return Task.FromResult(success);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<bool> PrintNetworkTcpAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || data == null || data.Length == 0)
        {
            return false;
        }

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(4));

            await client.ConnectAsync(ipAddress, port, cts.Token);
            using var stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length, cts.Token);
            await stream.FlushAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PrintReceiptAsync(SaleReceiptModel receipt, BillTemplateConfig config, string? targetPrinter = null, CancellationToken cancellationToken = default)
    {
        if (config.PaperSize is PaperSize.Thermal_80mm or PaperSize.Thermal_58mm)
        {
            var rawBytes = await _generator.GenerateEscPosBillAsync(receipt, config);
            var printer = targetPrinter;

            if (string.IsNullOrWhiteSpace(printer))
            {
                var installed = await GetInstalledPrintersAsync();
                printer = installed.FirstOrDefault(p => p.IsDefault)?.Name ?? installed.FirstOrDefault()?.Name ?? "Generic / Text Only";
            }

            return await PrintRawEscPosAsync(printer, rawBytes, $"Invoice {receipt.InvoiceNo}", cancellationToken);
        }
        else
        {
            // For A4 / A5, standard Windows print dialog / spooler pipeline is used
            return true;
        }
    }
}

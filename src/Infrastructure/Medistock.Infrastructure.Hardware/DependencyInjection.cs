using Microsoft.Extensions.DependencyInjection;

namespace Medistock.Infrastructure.Hardware;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureHardware(this IServiceCollection services)
    {
        services.AddSingleton<IReceiptPrinter, EscPosReceiptPrinter>();
        services.AddSingleton<ICashDrawer, EscPosReceiptPrinter>();
        services.AddSingleton<Medistock.Infrastructure.Hardware.Printers.IDocumentReportGenerator, Medistock.Infrastructure.Hardware.Printers.DocumentReportGenerator>();
        services.AddSingleton<Medistock.Infrastructure.Hardware.Printers.IBillDocumentGenerator, Medistock.Infrastructure.Hardware.Printers.BillDocumentGenerator>();
        services.AddSingleton<Medistock.Infrastructure.Hardware.Printers.IHardwarePrinterService, Medistock.Infrastructure.Hardware.Printers.HardwarePrinterService>();
        services.AddSingleton<Medistock.Infrastructure.Hardware.Export.IInventoryExportService, Medistock.Infrastructure.Hardware.Export.InventoryExportService>();
        services.AddSingleton<Medistock.Infrastructure.Hardware.Export.IPurchaseExportService, Medistock.Infrastructure.Hardware.Export.PurchaseExportService>();
        return services;
    }
}

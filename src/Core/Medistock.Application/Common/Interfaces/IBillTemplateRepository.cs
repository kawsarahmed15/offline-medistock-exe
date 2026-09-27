using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;

namespace Medistock.Application.Common.Interfaces;

public interface IBillTemplateRepository
{
    Task<BillTemplateConfig> GetDefaultTemplateAsync(CancellationToken cancellationToken = default);
    Task<BillTemplateConfig?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<BillTemplateConfig>> ListAllTemplatesAsync(CancellationToken cancellationToken = default);
    Task SaveTemplateAsync(BillTemplateConfig template, CancellationToken cancellationToken = default);
    Task SetDefaultTemplateAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(string id, CancellationToken cancellationToken = default);
}

public class PrinterConfigurationDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PrinterInterfaceType InterfaceType { get; set; }
    public string TargetNameOrIp { get; set; } = string.Empty;
    public int TargetPort { get; set; } = 9100;
    public PaperSize PaperSize { get; set; }
    public string? AssignedTemplateId { get; set; }
    public bool AutoCutPaper { get; set; } = true;
    public bool KickCashDrawer { get; set; } = true;
    public bool IsDefaultPos { get; set; }
    public bool IsDefaultA4 { get; set; }
}

public interface IPrinterConfigRepository
{
    Task<List<PrinterConfigurationDto>> ListPrinterConfigsAsync(CancellationToken cancellationToken = default);
    Task<PrinterConfigurationDto?> GetDefaultPosPrinterAsync(CancellationToken cancellationToken = default);
    Task<PrinterConfigurationDto?> GetDefaultA4PrinterAsync(CancellationToken cancellationToken = default);
    Task SavePrinterConfigAsync(PrinterConfigurationDto config, CancellationToken cancellationToken = default);
    Task DeletePrinterConfigAsync(string id, CancellationToken cancellationToken = default);
}

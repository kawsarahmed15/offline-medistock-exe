using System.Threading.Tasks;
using Medistock.Contracts.Printing;

namespace Medistock.Infrastructure.Hardware.Printers;

public interface IBillDocumentGenerator
{
    Task<string> GenerateHtmlBillAsync(SaleReceiptModel receipt, BillTemplateConfig config);
    Task<byte[]> GenerateEscPosBillAsync(SaleReceiptModel receipt, BillTemplateConfig config);
    Task<string> ConvertAmountToWordsInrAsync(decimal amount);
}

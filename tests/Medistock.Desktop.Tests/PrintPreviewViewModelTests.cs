using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Printing;
using Medistock.Desktop.ViewModels;
using Medistock.Infrastructure.Hardware;
using Medistock.Infrastructure.Hardware.Printers;
using Xunit;

namespace Medistock.Desktop.Tests;

public class PrintPreviewViewModelTests
{
    private class FakeTemplateRepo : IBillTemplateRepository
    {
        private BillTemplateConfig _template = BillTemplatePresets.CreateA4StandardPreset();

        public Task<BillTemplateConfig> GetDefaultTemplateAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(_template);

        public Task<BillTemplateConfig?> GetTemplateByIdAsync(string id, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<BillTemplateConfig?>(_template);

        public Task<List<BillTemplateConfig>> ListAllTemplatesAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<BillTemplateConfig> { _template });

        public Task SaveTemplateAsync(BillTemplateConfig template, System.Threading.CancellationToken cancellationToken = default)
        {
            _template = template;
            return Task.CompletedTask;
        }

        public Task SetDefaultTemplateAsync(string id, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteTemplateAsync(string id, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakePrinterService : IHardwarePrinterService
    {
        public Task<List<PrinterDeviceInfo>> GetInstalledPrintersAsync() =>
            Task.FromResult(new List<PrinterDeviceInfo> { new("EPSON TM-T82", true, false, "Ready") });

        public Task<bool> PrintRawEscPosAsync(string printerName, byte[] data, string docTitle = "Medistock Receipt", System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> PrintNetworkTcpAsync(string ipAddress, int port, byte[] data, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> PrintReceiptAsync(SaleReceiptModel receipt, BillTemplateConfig config, string? targetPrinter = null, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadTemplateAndHtml()
    {
        var sampleReceipt = new SaleReceiptModel(
            PharmacyName: "Apex Meds",
            PharmacyAddress: "Mumbai",
            PharmacyPhone: "12345",
            Gstin: "27AAA",
            DlNumbers: "DL1",
            InvoiceNo: "INV-001",
            InvoiceDate: DateTime.Now,
            CounterName: "C1",
            CashierName: "Raj",
            CustomerName: "John Doe",
            DoctorName: "Dr. Smith",
            Items: new List<ReceiptItemModel> { new("Dolo 650", "B1", DateTime.Now.AddYears(1), 1, 30m, 30m, 12m) },
            Subtotal: 30m,
            CgstAmount: 1.8m,
            SgstAmount: 1.8m,
            IgstAmount: 0m,
            RoundOff: 0m,
            GrandTotal: 33.6m,
            Payments: new List<ReceiptPaymentModel> { new("Cash", 33.6m, null) }
        );

        var generator = new BillDocumentGenerator();
        var repo = new FakeTemplateRepo();
        var printerService = new FakePrinterService();

        var vm = new PrintPreviewViewModel(generator, repo, printerService);
        await vm.InitializeAsync(sampleReceipt);

        Assert.NotNull(vm.CurrentHtmlContent);
        Assert.Contains("INV-001", vm.CurrentHtmlContent);
        Assert.Contains("John Doe", vm.CurrentHtmlContent);
        Assert.NotEmpty(vm.InstalledPrinters);
    }
}

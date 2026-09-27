using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Printing;
using Medistock.Desktop.ViewModels;
using Medistock.Infrastructure.Hardware.Printers;
using Xunit;

namespace Medistock.Desktop.Tests;

public class BillCustomizerViewModelTests
{
    private class FakeTemplateRepo : IBillTemplateRepository, IPrinterConfigRepository
    {
        private List<BillTemplateConfig> _templates = new()
        {
            BillTemplatePresets.CreateA4StandardPreset(),
            BillTemplatePresets.CreateThermal80mmPreset()
        };

        public Task<BillTemplateConfig> GetDefaultTemplateAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(_templates.First(t => t.IsDefault));

        public Task<BillTemplateConfig?> GetTemplateByIdAsync(string id, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));

        public Task<List<BillTemplateConfig>> ListAllTemplatesAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(_templates);

        public Task SaveTemplateAsync(BillTemplateConfig template, System.Threading.CancellationToken cancellationToken = default)
        {
            var idx = _templates.FindIndex(t => t.Id == template.Id);
            if (idx >= 0) _templates[idx] = template;
            else _templates.Add(template);
            return Task.CompletedTask;
        }

        public Task SetDefaultTemplateAsync(string id, System.Threading.CancellationToken cancellationToken = default)
        {
            foreach (var t in _templates) t.IsDefault = (t.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteTemplateAsync(string id, System.Threading.CancellationToken cancellationToken = default)
        {
            _templates.RemoveAll(t => t.Id == id);
            return Task.CompletedTask;
        }

        public Task<List<PrinterConfigurationDto>> ListPrinterConfigsAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<PrinterConfigurationDto>());

        public Task<PrinterConfigurationDto?> GetDefaultPosPrinterAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<PrinterConfigurationDto?>(null);

        public Task<PrinterConfigurationDto?> GetDefaultA4PrinterAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<PrinterConfigurationDto?>(null);

        public Task SavePrinterConfigAsync(PrinterConfigurationDto config, System.Threading.CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeletePrinterConfigAsync(string id, System.Threading.CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadSelectedTemplateAndColumns()
    {
        var repo = new FakeTemplateRepo();
        var generator = new BillDocumentGenerator();
        var vm = new BillCustomizerViewModel(repo, generator);

        await vm.InitializeAsync();

        Assert.NotNull(vm.CurrentTemplate);
        Assert.Contains("Standard A4 Tax Invoice", vm.CurrentTemplate.Name);
        Assert.Equal(19, vm.CurrentTemplate.Columns.Count);
        Assert.NotNull(vm.LivePreviewHtml);
        Assert.Contains(vm.CurrentTemplate.Header.StoreName, vm.LivePreviewHtml);
    }

    [Fact]
    public async Task MoveColumnUp_ShouldSwapDisplayOrders()
    {
        var repo = new FakeTemplateRepo();
        var generator = new BillDocumentGenerator();
        var vm = new BillCustomizerViewModel(repo, generator);

        await vm.InitializeAsync();

        var col2 = vm.CurrentTemplate!.Columns.First(c => c.DisplayOrder == 2);
        vm.MoveColumnUp(col2);

        var updatedCol = vm.CurrentTemplate.Columns.First(c => c.FieldType == col2.FieldType);
        Assert.Equal(1, updatedCol.DisplayOrder);
    }

    [Fact]
    public async Task SwitchPreset_ShouldLoadNewPresetProperties()
    {
        var repo = new FakeTemplateRepo();
        var generator = new BillDocumentGenerator();
        var vm = new BillCustomizerViewModel(repo, generator);

        await vm.InitializeAsync();
        await vm.ApplyPresetAsync(PaperSize.Thermal_80mm);

        Assert.Equal(PaperSize.Thermal_80mm, vm.CurrentTemplate!.PaperSize);
    }
}

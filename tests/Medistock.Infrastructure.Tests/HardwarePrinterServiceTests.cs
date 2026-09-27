using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Medistock.Contracts.Printing;
using Medistock.Infrastructure.Hardware;
using Medistock.Infrastructure.Hardware.Printers;
using Xunit;

namespace Medistock.Infrastructure.Tests;

public class HardwarePrinterServiceTests
{
    private readonly HardwarePrinterService _service;
    private readonly BillDocumentGenerator _generator;

    public HardwarePrinterServiceTests()
    {
        _generator = new BillDocumentGenerator();
        _service = new HardwarePrinterService(_generator);
    }

    [Fact]
    public async Task GetInstalledPrinters_ShouldReturnNonEmptyOrMockPrinters()
    {
        var printers = await _service.GetInstalledPrintersAsync();

        Assert.NotNull(printers);
        // On Windows machines, standard Microsoft Print to PDF or similar exists or fallback mock
        Assert.NotEmpty(printers);
    }

    [Fact]
    public async Task PrintRawEscPos_WithValidDummyStream_ShouldHandleGracefully()
    {
        var dummyBytes = new byte[] { 0x1B, 0x40, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x0A };
        // Testing with nonexistent printer should return false/result without throwing unhandled crash
        var success = await _service.PrintRawEscPosAsync("NonExistent_Test_Printer_123", dummyBytes);
        
        // Expected false because printer does not exist on test machine
        Assert.False(success);
    }
}

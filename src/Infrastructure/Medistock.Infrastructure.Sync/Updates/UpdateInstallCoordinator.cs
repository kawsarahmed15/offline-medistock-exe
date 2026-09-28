using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Medistock.Infrastructure.Sync.Updates;

public interface IUpdateInstallCoordinator
{
    Task<bool> InstallUpdateAsync(string installerPath, CancellationToken ct = default);
}

public class UpdateInstallCoordinator : IUpdateInstallCoordinator
{
    private const string PipeName = "MedistockUpdaterPipe";
    private readonly ILogger<UpdateInstallCoordinator>? _logger;

    public UpdateInstallCoordinator(ILogger<UpdateInstallCoordinator>? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> InstallUpdateAsync(string installerPath, CancellationToken ct = default)
    {
        if (!File.Exists(installerPath))
        {
            _logger?.LogError("Cannot install update: file not found at {Path}", installerPath);
            return false;
        }

        // 1. Try communicating via silent Updater Windows Service (SYSTEM account, 0 UAC prompts)
        try
        {
            _logger?.LogInformation("Attempting silent update via Updater Service Named Pipe...");
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await client.ConnectAsync(linkedCts.Token);

            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"INSTALL:{installerPath}");
            var response = await reader.ReadLineAsync(ct);

            if (string.Equals(response, "DONE", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("Silent update completed successfully via service.");
                return true;
            }

            _logger?.LogWarning("Service returned unexpected response: {Resp}", response);
        }
        catch (Exception ex)
        {
            _logger?.LogInformation("Updater service unavailable ({Msg}). Falling back to elevated execution...", ex.Message);
        }

        // 2. Fallback: Launch installer directly with elevation (UAC prompt)
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
                Verb = "runas" // Request elevation
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to launch installer fallback.");
            return false;
        }
    }
}

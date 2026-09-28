using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Medistock.UpdaterService;

/// <summary>
/// Background worker that listens on a local Named Pipe for update installation commands.
/// Runs under the SYSTEM service account to install updates silently without prompting UAC.
/// </summary>
public class UpdaterWorker : BackgroundService
{
    private const string PipeName = "MedistockUpdaterPipe";
    private readonly ILogger<UpdaterWorker> _logger;

    public UpdaterWorker(ILogger<UpdaterWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Medistock Updater Service pipe listener started on pipe: {Pipe}", PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(stoppingToken);
                _logger.LogInformation("Client connected to updater named pipe.");

                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                var command = await reader.ReadLineAsync(stoppingToken);
                _logger.LogInformation("Received command: {Cmd}", command);

                if (!string.IsNullOrWhiteSpace(command) && command.StartsWith("INSTALL:", StringComparison.OrdinalIgnoreCase))
                {
                    var installerPath = command.Substring("INSTALL:".Length).Trim();
                    var success = await ExecuteInstallerAsync(installerPath, stoppingToken);

                    if (success)
                    {
                        await writer.WriteLineAsync("DONE");
                        _logger.LogInformation("Update installation succeeded.");
                    }
                    else
                    {
                        await writer.WriteLineAsync("FAILED:Installer returned non-zero exit code.");
                        _logger.LogWarning("Update installation failed.");
                    }
                }
                else
                {
                    await writer.WriteLineAsync("UNKNOWN_COMMAND");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling named pipe connection.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Medistock Updater Service pipe listener stopped.");
    }

    private async Task<bool> ExecuteInstallerAsync(string installerPath, CancellationToken ct)
    {
        if (!File.Exists(installerPath))
        {
            _logger.LogError("Installer file not found at: {Path}", installerPath);
            return false;
        }

        try
        {
            _logger.LogInformation("Launching silent installer: {Path}", installerPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start installer process.");
                return false;
            }

            await process.WaitForExitAsync(ct);
            _logger.LogInformation("Installer process exited with code: {Code}", process.ExitCode);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception running silent installer.");
            return false;
        }
    }
}

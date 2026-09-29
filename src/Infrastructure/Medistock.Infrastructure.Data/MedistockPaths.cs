using System;
using System.IO;

namespace Medistock.Infrastructure.Data;

/// <summary>
/// Single source of truth for all Medistock data paths on the local machine.
/// All paths live under %LOCALAPPDATA%\Medistock\ — isolated per Windows user,
/// survives app reinstalls, and never writes to the install directory.
/// </summary>
public static class MedistockPaths
{
    // Root: C:\Users\<username>\AppData\Local\Medistock\
    public static string AppDataRoot { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Medistock");

    // Main SQLite database
    public static string Database { get; } =
        Path.Combine(AppDataRoot, "Data", "medistock_local.db");

    // App settings (theme, font size, etc.)
    public static string SettingsFile { get; } =
        Path.Combine(AppDataRoot, "Config", "user_settings.json");

    // Activation / license token metadata (non-sensitive; the actual JWT is in PasswordVault)
    public static string ActivationMetadataFile { get; } =
        Path.Combine(AppDataRoot, "Config", "activation.json");

    // Application logs (NOT the install directory)
    public static string LogsDirectory { get; } =
        Path.Combine(AppDataRoot, "Logs");

    public static string StartupLog { get; } =
        Path.Combine(LogsDirectory, "startup.log");

    // Update staging: downloaded installer stored here before silent install
    public static string UpdateStagingDirectory { get; } =
        Path.Combine(AppDataRoot, "Updates");

    // Default local backup exports — stored in user's Documents (user-visible)
    public static string DefaultBackupsDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Medistock", "Backups");

    // Local backup exports — dynamically resolved from user settings if specified, otherwise falls back to DefaultBackupsDirectory
    public static string BackupsDirectory
    {
        get
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("BackupLocation", out var bp))
                    {
                        var custom = bp.GetString();
                        if (!string.IsNullOrWhiteSpace(custom))
                        {
                            return custom;
                        }
                    }
                }
            }
            catch { }
            return DefaultBackupsDirectory;
        }
    }

    // Temp: in-progress operations (backup creation, restore staging)
    public static string TempDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "Medistock");

    /// <summary>
    /// Call once at application startup to ensure all required directories exist.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public static void EnsureAllDirectoriesExist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(UpdateStagingDirectory);
        try
        {
            Directory.CreateDirectory(BackupsDirectory);
        }
        catch { }
        Directory.CreateDirectory(TempDirectory);
    }
}

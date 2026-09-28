using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace Medistock.Installer;

public static class InstallerEngine
{
    public static string DefaultInstallPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Medistock");

    public static string ExecutablePath(string installDir) =>
        Path.Combine(installDir, "Medistock.Desktop.exe");

    public static void Install(string targetDir, IProgress<(int Percent, string Status)>? progress)
    {
        progress?.Report((5, "Preparing destination directory..."));
        Directory.CreateDirectory(targetDir);

        // 1. Extract embedded payload zip
        progress?.Report((15, "Extracting application binaries and UI components..."));
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("MedistockPayload.zip");
        if (stream == null)
        {
            throw new InvalidOperationException("Embedded payload 'MedistockPayload.zip' could not be found.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        int totalEntries = archive.Entries.Count;
        int extracted = 0;

        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            if (!destinationPath.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Guard against zip traversal
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
            }
            else
            {
                var dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                entry.ExtractToFile(destinationPath, overwrite: true);
            }

            extracted++;
            int currentPct = 15 + (int)((extracted / (double)totalEntries) * 65);
            progress?.Report((currentPct, $"Extracting: {entry.Name}"));
        }

        // 2. Create Desktop Shortcut
        progress?.Report((85, "Creating Desktop shortcut..."));
        var exePath = ExecutablePath(targetDir);
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var desktopShortcut = Path.Combine(desktopPath, "Medistock.lnk");
        CreateShortcut(desktopShortcut, exePath, targetDir, "Medistock Pharmacy Management System");

        // 3. Create Start Menu Shortcut
        progress?.Report((90, "Creating Start Menu shortcut..."));
        var startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        Directory.CreateDirectory(startMenuPath);
        var startMenuShortcut = Path.Combine(startMenuPath, "Medistock.lnk");
        CreateShortcut(startMenuShortcut, exePath, targetDir, "Medistock Pharmacy Management System");

        // 4. Register Uninstaller in Windows Registry
        progress?.Report((95, "Registering application in Windows Programs..."));
        RegisterUninstall(targetDir, exePath);

        // 5. Create Uninstall script helper
        CreateUninstallScript(targetDir);

        progress?.Report((100, "Installation completed successfully!"));
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    shortcut.IconLocation = $"{targetPath},0";
                    shortcut.Save();
                }
            }
        }
        catch { }
    }

    private static void RegisterUninstall(string installDir, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Medistock");
            if (key != null)
            {
                key.SetValue("DisplayName", "Medistock Pharmacy Management");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "Medistock Team");
                key.SetValue("DisplayIcon", $"{exePath},0");
                key.SetValue("InstallLocation", installDir);
                var uninstallerPath = Path.Combine(installDir, "Uninstall.cmd");
                key.SetValue("UninstallString", $"\"{uninstallerPath}\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    private static void CreateUninstallScript(string installDir)
    {
        try
        {
            var cmdContent = $@"@echo off
taskkill /F /IM Medistock.Desktop.exe 2>nul
del /Q ""{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Medistock.lnk")}"" 2>nul
del /Q ""{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Medistock.lnk")}"" 2>nul
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Medistock"" /f 2>nul
echo Uninstalled Medistock. Removing files...
start /b cmd /c ""timeout /t 1 >nul & rmdir /S /Q """"{installDir}""""""
";
            File.WriteAllText(Path.Combine(installDir, "Uninstall.cmd"), cmdContent);
        }
        catch { }
    }

    public static void PerformUninstall()
    {
        try
        {
            var dir = DefaultInstallPath;
            var desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Medistock.lnk");
            var startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Medistock.lnk");

            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);
            if (File.Exists(startMenuLnk)) File.Delete(startMenuLnk);

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Medistock", throwOnMissingSubKey: false);

            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 1 >nul & rmdir /S /Q \"{dir}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }
        catch { }
    }
}

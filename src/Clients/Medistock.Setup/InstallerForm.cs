using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Medistock.Setup;

public class InstallerForm : Form
{
    private readonly TextBox _txtInstallDir;
    private readonly Button _btnBrowse;
    private readonly CheckBox _chkDesktopShortcut;
    private readonly CheckBox _chkStartMenuShortcut;
    private readonly CheckBox _chkLaunchAfter;
    private readonly ProgressBar _progressBar;
    private readonly Label _lblStatus;
    private readonly Button _btnInstall;
    private readonly Button _btnCancel;
    private bool _isCompleted = false;

    public InstallerForm()
    {
        Text = "Medistock Setup — Pharmacy ERP & POS";
        Size = new Size(580, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 249, 250);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Load Icon
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var iconStream = asm.GetManifestResourceStream("Medistock.Setup.AppIcon.ico");
            if (iconStream != null)
            {
                Icon = new Icon(iconStream);
            }
        }
        catch { }

        // Top Header Banner
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = Color.FromArgb(15, 23, 42), // Slate 900
            Padding = new Padding(20, 15, 20, 15)
        };

        // Logo in Header
        var pbLogo = new PictureBox
        {
            Size = new Size(56, 56),
            Location = new Point(20, 16),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var logoStream = asm.GetManifestResourceStream("Medistock.Setup.MedistockLogo.png");
            if (logoStream != null)
            {
                pbLogo.Image = Image.FromStream(logoStream);
            }
        }
        catch { }
        headerPanel.Controls.Add(pbLogo);

        var lblHeaderTitle = new Label
        {
            Text = "Medistock Pharmacy ERP & POS",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(88, 18),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblHeaderTitle);

        var lblHeaderSub = new Label
        {
            Text = "Version 1.0.0 (Offline-First Edition) Setup Wizard",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(90, 48),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblHeaderSub);
        Controls.Add(headerPanel);

        // Main Content Area
        var contentPanel = new Panel
        {
            Location = new Point(24, 106),
            Size = new Size(516, 220)
        };

        var lblDestination = new Label
        {
            Text = "Installation Destination Folder:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(0, 8),
            AutoSize = true
        };
        contentPanel.Controls.Add(lblDestination);

        var defaultInstallPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Medistock");

        _txtInstallDir = new TextBox
        {
            Text = defaultInstallPath,
            Location = new Point(0, 32),
            Size = new Size(410, 28),
            Font = new Font("Segoe UI", 9.5f)
        };
        contentPanel.Controls.Add(_txtInstallDir);

        _btnBrowse = new Button
        {
            Text = "Browse...",
            Location = new Point(418, 30),
            Size = new Size(96, 30),
            UseVisualStyleBackColor = true
        };
        _btnBrowse.Click += BtnBrowse_Click;
        contentPanel.Controls.Add(_btnBrowse);

        // Options Checkboxes
        _chkDesktopShortcut = new CheckBox
        {
            Text = "Create Desktop Shortcut (Recommended)",
            Checked = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Location = new Point(4, 76),
            Size = new Size(450, 24)
        };
        contentPanel.Controls.Add(_chkDesktopShortcut);

        _chkStartMenuShortcut = new CheckBox
        {
            Text = "Create Start Menu Shortcut",
            Checked = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Location = new Point(4, 104),
            Size = new Size(450, 24)
        };
        contentPanel.Controls.Add(_chkStartMenuShortcut);

        _chkLaunchAfter = new CheckBox
        {
            Text = "Launch Medistock when setup finishes",
            Checked = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Location = new Point(4, 132),
            Size = new Size(450, 24)
        };
        contentPanel.Controls.Add(_chkLaunchAfter);

        _lblStatus = new Label
        {
            Text = "Ready to install. Click Install to begin.",
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(2, 164),
            Size = new Size(510, 20),
            AutoEllipsis = true
        };
        contentPanel.Controls.Add(_lblStatus);

        _progressBar = new ProgressBar
        {
            Location = new Point(2, 188),
            Size = new Size(512, 22),
            Style = ProgressBarStyle.Continuous,
            Value = 0
        };
        contentPanel.Controls.Add(_progressBar);
        Controls.Add(contentPanel);

        // Bottom Action Bar
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 12, 24, 12)
        };

        _btnCancel = new Button
        {
            Text = "Cancel",
            Size = new Size(90, 34),
            Location = new Point(340, 13),
            UseVisualStyleBackColor = true
        };
        _btnCancel.Click += (s, e) => Close();
        bottomPanel.Controls.Add(_btnCancel);

        _btnInstall = new Button
        {
            Text = "Install",
            Size = new Size(100, 34),
            Location = new Point(438, 13),
            BackColor = Color.FromArgb(14, 165, 233), // Sky Blue Accent
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += BtnInstall_Click;
        bottomPanel.Controls.Add(_btnInstall);

        Controls.Add(bottomPanel);
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Select Installation Directory for Medistock",
            SelectedPath = _txtInstallDir.Text,
            UseDescriptionForTitle = true
        };
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            _txtInstallDir.Text = fbd.SelectedPath;
        }
    }

    private async void BtnInstall_Click(object? sender, EventArgs e)
    {
        if (_isCompleted)
        {
            Close();
            return;
        }

        var targetDir = _txtInstallDir.Text.Trim();
        if (string.IsNullOrEmpty(targetDir))
        {
            MessageBox.Show("Please specify a valid installation directory.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnInstall.Enabled = false;
        _btnBrowse.Enabled = false;
        _txtInstallDir.Enabled = false;
        _chkDesktopShortcut.Enabled = false;
        _chkStartMenuShortcut.Enabled = false;
        _btnCancel.Enabled = false;

        _lblStatus.Text = "Preparing installation...";
        _progressBar.Value = 5;

        try
        {
            await Task.Run(() => PerformInstallation(targetDir));

            _progressBar.Value = 100;
            _lblStatus.Text = "Installation complete! Medistock is ready.";
            _lblStatus.ForeColor = Color.FromArgb(16, 185, 129); // Emerald green

            _isCompleted = true;
            _btnInstall.Enabled = true;
            _btnInstall.Text = "Finish";
            _btnCancel.Visible = false;

            if (_chkLaunchAfter.Checked)
            {
                var exePath = Path.Combine(targetDir, "Medistock.Desktop.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Installation failed: " + ex.Message;
            _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
            _btnInstall.Enabled = true;
            _btnCancel.Enabled = true;
            MessageBox.Show("Installation encountered an error:\n\n" + ex.Message, "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PerformInstallation(string targetDir)
    {
        // 1. Terminate any running Medistock instances first so files aren't locked
        try
        {
            foreach (var proc in Process.GetProcessesByName("Medistock.Desktop"))
            {
                try { proc.Kill(); proc.WaitForExit(2000); } catch { }
            }
            foreach (var proc in Process.GetProcessesByName("Medistock"))
            {
                try { proc.Kill(); proc.WaitForExit(2000); } catch { }
            }
        }
        catch { }

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 2. Extract payload
        var asm = Assembly.GetExecutingAssembly();
        using (var zipStream = asm.GetManifestResourceStream("Medistock.Setup.app_payload.zip"))
        {
            if (zipStream == null)
            {
                throw new FileNotFoundException("Embedded application payload was not found in setup executable.");
            }

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            int total = archive.Entries.Count;
            int count = 0;

            foreach (var entry in archive.Entries)
            {
                // Skip WebView2 cache directories
                if (entry.FullName.Contains(".WebView2/", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.Contains(".WebView2\\", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.Contains("EBWebView", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    continue;
                }

                var normalizedPath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);

                // Directory entry check
                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    var dir = Path.Combine(targetDir, normalizedPath);
                    Directory.CreateDirectory(dir);
                    continue;
                }

                var destPath = Path.Combine(targetDir, normalizedPath);
                var parentDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                // Extract file with retry logic in case of transient file system locks
                bool extracted = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        entry.ExtractToFile(destPath, overwrite: true);
                        extracted = true;
                        break;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        System.Threading.Thread.Sleep(300);
                    }
                }

                if (!extracted)
                {
                    entry.ExtractToFile(destPath, overwrite: true);
                }

                count++;

                if (count % 10 == 0 || count == total)
                {
                    int pct = 5 + (int)((count / (double)total) * 85);
                    Invoke(new Action(() =>
                    {
                        _progressBar.Value = Math.Min(95, pct);
                        _lblStatus.Text = $"Extracting: {entry.Name} ({count}/{total})";
                    }));
                }
            }
        }

        var mainExe = Path.Combine(targetDir, "Medistock.Desktop.exe");
        var iconPath = Path.Combine(targetDir, "Assets", "AppIcon.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = mainExe;
        }

        // Create Desktop Shortcut
        if (_chkDesktopShortcut.Checked)
        {
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var desktopLnk = Path.Combine(desktopDir, "Medistock.lnk");
            CreateShortcut(desktopLnk, mainExe, iconPath, "Medistock — Pharmacy ERP & POS");
        }

        // Create Start Menu Shortcut
        if (_chkStartMenuShortcut.Checked)
        {
            var startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                "Medistock");
            if (!Directory.Exists(startMenuDir))
            {
                Directory.CreateDirectory(startMenuDir);
            }
            var startLnk = Path.Combine(startMenuDir, "Medistock.lnk");
            CreateShortcut(startLnk, mainExe, iconPath, "Medistock — Pharmacy ERP & POS");
        }

        // Register in Windows Add/Remove Programs (Registry)
        RegisterUninstallInfo(targetDir, mainExe, iconPath);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string iconPath, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = description;
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath + ",0";
                }
                shortcut.Save();
            }
        }
        catch { }
    }

    private static void RegisterUninstallInfo(string targetDir, string mainExe, string iconPath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Medistock");
            if (key != null)
            {
                key.SetValue("DisplayName", "Medistock — Pharmacy ERP & POS");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "Medistock Health Technologies");
                key.SetValue("DisplayIcon", iconPath);
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{targetDir}\"");
            }
        }
        catch { }
    }
}

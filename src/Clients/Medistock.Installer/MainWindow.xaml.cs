using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Medistock.Installer;

public partial class MainWindow : Window
{
    private bool _isInstalled = false;
    private bool _isInstalling = false;
    private readonly string _installDir = InstallerEngine.DefaultInstallPath;

    public MainWindow()
    {
        InitializeComponent();
        StageDetailText.Text = $"Medistock will be installed into:\n{_installDir}\n\nDesktop and Start Menu shortcuts will be created automatically.";
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalled)
        {
            // Finish / Launch
            if (LaunchAfterInstallCheck.IsChecked == true)
            {
                var exePath = InstallerEngine.ExecutablePath(_installDir);
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
            }
            Close();
            return;
        }

        if (_isInstalling) return;

        _isInstalling = true;
        ActionButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        StageTitleText.Text = "Installing Medistock...";

        var progress = new Progress<(int Percent, string Status)>(update =>
        {
            InstallProgressBar.Value = update.Percent;
            StatusLogText.Text = update.Status;
        });

        try
        {
            await Task.Run(() => InstallerEngine.Install(_installDir, progress));

            _isInstalled = true;
            _isInstalling = false;
            StageTitleText.Text = "Installation Complete! 🎉";
            StageDetailText.Text = "Medistock has been installed successfully. You can now launch it or find it on your Desktop and Start Menu.";
            StatusLogText.Text = "Ready to use.";
            InstallProgressBar.Value = 100;
            InstallProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));

            LaunchAfterInstallCheck.Visibility = Visibility.Visible;
            ActionButton.Content = "Finish";
            ActionButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _isInstalling = false;
            ActionButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            StageTitleText.Text = "Installation Failed";
            StageDetailText.Text = $"An error occurred during installation:\n{ex.Message}";
            StatusLogText.Text = "Failed.";
            InstallProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

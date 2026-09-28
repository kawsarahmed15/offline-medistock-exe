using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Medistock.Installer;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("/uninstall") || e.Args.Contains("--uninstall"))
        {
            InstallerEngine.PerformUninstall();
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}

using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Dispatching;

namespace Medistock.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
        try
        {
            File.WriteAllText(logFile, $"Starting Medistock.Desktop at {DateTime.UtcNow:O}\n");

            try
            {
                Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(0x00010006, out _);
            }
            catch { }

            WinRT.ComWrappersSupport.InitializeComWrappers();
            global::Microsoft.UI.Xaml.Application.Start((p) =>
            {
                try
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logFile, $"FATAL in Application.Start: {ex}\n");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile, $"FATAL in Main: {ex}\n");
        }
    }
}


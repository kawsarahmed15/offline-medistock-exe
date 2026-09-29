using System;
using Medistock.Desktop.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Medistock.Desktop.Views.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }
    public System.Action? OpenCustomizerRequested { get; set; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }

    private void OpenCustomizer_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (OpenCustomizerRequested != null)
        {
            OpenCustomizerRequested.Invoke();
            return;
        }

        if (this.Frame != null)
        {
            this.Frame.Navigate(typeof(BillCustomizerPage));
        }
    }

    private async void BrowseBackupLocation_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            if (App.MainWindowInstance != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            }

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && !string.IsNullOrWhiteSpace(folder.Path))
            {
                ViewModel.BackupLocation = folder.Path;
                ViewModel.RefreshBackupsList();
            }
        }
        catch (Exception ex)
        {
            ViewModel.BackupStatusMessage = $"Could not pick folder: {ex.Message}";
        }
    }

    private async void BrowseAndRestoreLocalBackup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".zip");

            if (App.MainWindowInstance != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            }

            var file = await openPicker.PickSingleFileAsync();
            if (file != null && !string.IsNullOrWhiteSpace(file.Path))
            {
                var dialog = new ContentDialog
                {
                    Title = "⚠️ Confirm Database Restore",
                    Content = $"Restoring from '{file.Name}' will overwrite your current active database with all data from this backup.\n\nAre you sure you want to proceed?",
                    PrimaryButtonText = "Yes, Restore Database",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.RestoreLocalBackupAsync(file.Path);
                }
            }
        }
        catch (Exception ex)
        {
            ViewModel.BackupStatusMessage = $"Restore operation error: {ex.Message}";
        }
    }

    private async void RestoreCloudBackup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "☁️ Confirm Cloud Backup Restore",
                Content = "Restoring from cloud will download the latest encrypted snapshot from https://offline-medistock.teklin.in, decrypt it locally, and replace your current database.\n\nAre you sure you want to proceed?",
                PrimaryButtonText = "Yes, Download & Restore",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.RestoreCloudBackupAsync((string?)null);
            }
        }
        catch (Exception ex)
        {
            ViewModel.BackupStatusMessage = $"Cloud restore error: {ex.Message}";
        }
    }

    private async void BrowseMedicineJson_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            openPicker.FileTypeFilter.Add(".json");

            if (App.MainWindowInstance != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            }

            var file = await openPicker.PickSingleFileAsync();
            if (file != null && !string.IsNullOrWhiteSpace(file.Path))
            {
                ViewModel.SetMedicineJsonPath(file.Path);
            }
        }
        catch (Exception ex)
        {
            ViewModel.SeedStatusMessage = $"Could not pick file: {ex.Message}";
        }
    }

    private async void ClearProducts_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "⚠️ Confirm Catalog Reset",
                Content = $"This will permanently delete all {ViewModel.ProductCount:N0} products, barcodes, batches, and stock balances from your local catalog.\n\nAre you sure you want to proceed?",
                PrimaryButtonText = "Yes, Clear Catalog",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.ClearMedicinesCatalogAsync();
            }
        }
        catch (Exception ex)
        {
            ViewModel.SeedStatusMessage = $"Clear operation error: {ex.Message}";
        }
    }
}

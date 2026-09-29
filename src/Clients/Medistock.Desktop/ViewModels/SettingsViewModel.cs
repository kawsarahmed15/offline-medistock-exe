using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Backup;
using Medistock.Contracts.Updates;
using Medistock.Infrastructure.Data;
using Medistock.Infrastructure.Data.Backup;
using Medistock.Infrastructure.Identity.Services;
using Medistock.Infrastructure.Sync.Backup;
using Medistock.Infrastructure.Sync.Updates;

namespace Medistock.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly string SettingsFilePath = MedistockPaths.SettingsFile;

    private readonly ILocalBackupService? _localBackupService;
    private readonly ICloudBackupService? _cloudBackupService;
    private readonly IUpdateService? _updateService;
    private readonly IActivationService? _activationService;
    private readonly IMedicineCatalogSeeder? _medicineCatalogSeeder;

    [ObservableProperty]
    private string _theme = "Dark";

    [ObservableProperty]
    private string _fontSize = "Standard (13px)";

    [ObservableProperty]
    private string _pharmacyName = "Medistock Pharmacy & Super Speciality";

    [ObservableProperty]
    private string _gstin = "07AAAAA0000A1Z5";

    [ObservableProperty]
    private string _contactPhone = "+91 98765 43210";

    [ObservableProperty]
    private string _storeAddress = "Shop No 12, Medical Market, Sector 14";

    [ObservableProperty]
    private string _invoicePrefix = "INV-2026-";

    [ObservableProperty]
    private bool _enableBarcodeAudio = true;

    [ObservableProperty]
    private bool _autoDailyBackup = true;

    [ObservableProperty]
    private bool _autoDeleteBackupsOlderThan7Days = true;

    [ObservableProperty]
    private int _nearExpiryDays = 90;

    [ObservableProperty]
    private decimal _defaultGstRate = 5.0m;

    public double DefaultGstRateDouble
    {
        get => (double)DefaultGstRate;
        set => DefaultGstRate = (decimal)value;
    }

    partial void OnDefaultGstRateChanged(decimal value)
    {
        OnPropertyChanged(nameof(DefaultGstRateDouble));
    }

    [ObservableProperty]
    private string _backupLocation = MedistockPaths.BackupsDirectory;

    [ObservableProperty]
    private string _statusMessage = "Settings loaded.";

    [ObservableProperty]
    private string _backupStatusMessage = "";

    [ObservableProperty]
    private string _updateStatusMessage = "";

    [ObservableProperty]
    private string _licenseInfo = "Activated";

    [ObservableProperty]
    private bool _isBusy;

    // --- Master Medicine Catalog Seeder Properties ---
    [ObservableProperty]
    private int _productCount;

    [ObservableProperty]
    private bool _hasProducts;

    [ObservableProperty]
    private string _medicineJsonPath = "";

    [ObservableProperty]
    private bool _isJsonFileDetected;

    [ObservableProperty]
    private bool _isSeeding;

    [ObservableProperty]
    private double _seedProgressPercent;

    [ObservableProperty]
    private string _seedProgressStatus = "";

    [ObservableProperty]
    private string _seedStatusMessage = "";

    public ObservableCollection<BackupFileInfo> LocalBackups { get; } = new();
    public ObservableCollection<CloudBackupInfo> CloudBackups { get; } = new();

    public Action<string>? ThemeChangedCallback { get; set; }
    public Action<string>? FontSizeChangedCallback { get; set; }

    public SettingsViewModel(
        ILocalBackupService? localBackupService = null,
        ICloudBackupService? cloudBackupService = null,
        IUpdateService? updateService = null,
        IActivationService? activationService = null,
        IMedicineCatalogSeeder? medicineCatalogSeeder = null)
    {
        _localBackupService = localBackupService;
        _cloudBackupService = cloudBackupService;
        _updateService = updateService;
        _activationService = activationService;
        _medicineCatalogSeeder = medicineCatalogSeeder;

        InitMedicinePath();
        LoadSettings();
        RefreshBackupsList();
        RefreshLicenseInfo();
        _ = RefreshProductCountAsync();
        _ = RefreshCloudBackupsListAsync();
        _ = PerformAutomatedBackupMaintenanceAsync();
    }

    private void InitMedicinePath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultPath = Path.Combine(userProfile, "Downloads", "indian_medicine_data.json");

        if (File.Exists(defaultPath))
        {
            MedicineJsonPath = defaultPath;
            IsJsonFileDetected = true;
        }
        else if (File.Exists(@"c:\Users\ahmed\Downloads\indian_medicine_data.json"))
        {
            MedicineJsonPath = @"c:\Users\ahmed\Downloads\indian_medicine_data.json";
            IsJsonFileDetected = true;
        }
        else
        {
            MedicineJsonPath = defaultPath;
            IsJsonFileDetected = false;
        }
    }

    public static int GetNearExpiryDays()
    {
        return GetStoreInfo().NearExpiryDays;
    }

    public static decimal GetDefaultGstRate()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("DefaultGstRate", out var dg) && dg.GetDecimal() >= 0)
                {
                    return dg.GetDecimal();
                }
            }
        }
        catch { }
        return 5.0m;
    }

    public static (string PharmacyName, string StoreAddress, string ContactPhone, string Gstin, int NearExpiryDays) GetStoreInfo()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string pharmacy = root.TryGetProperty("PharmacyName", out var pp) ? (pp.GetString() ?? "Medistock Pharmacy") : "Medistock Pharmacy";
                string address = root.TryGetProperty("StoreAddress", out var sa) ? (sa.GetString() ?? "Medical Market") : "Medical Market";
                string phone = root.TryGetProperty("ContactPhone", out var ph) ? (ph.GetString() ?? "+91 98765 43210") : "+91 98765 43210";
                string gstin = root.TryGetProperty("Gstin", out var gp) ? (gp.GetString() ?? "07AAAAA0000A1Z5") : "07AAAAA0000A1Z5";
                int nearExp = root.TryGetProperty("NearExpiryDays", out var ned) && ned.GetInt32() > 0 ? ned.GetInt32() : 90;

                return (pharmacy, address, phone, gstin, nearExp);
            }
        }
        catch { }
        return ("Medistock Pharmacy", "Medical Market", "+91 98765 43210", "07AAAAA0000A1Z5", 90);
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Theme", out var tp)) Theme = tp.GetString() ?? "Dark";
                if (root.TryGetProperty("FontSize", out var fp)) FontSize = fp.GetString() ?? "Standard (13px)";
                if (root.TryGetProperty("PharmacyName", out var pp)) PharmacyName = pp.GetString() ?? PharmacyName;
                if (root.TryGetProperty("Gstin", out var gp)) Gstin = gp.GetString() ?? Gstin;
                if (root.TryGetProperty("ContactPhone", out var ph)) ContactPhone = ph.GetString() ?? ContactPhone;
                if (root.TryGetProperty("StoreAddress", out var sa)) StoreAddress = sa.GetString() ?? StoreAddress;
                if (root.TryGetProperty("InvoicePrefix", out var ip)) InvoicePrefix = ip.GetString() ?? InvoicePrefix;
                if (root.TryGetProperty("EnableBarcodeAudio", out var ea)) EnableBarcodeAudio = ea.GetBoolean();
                if (root.TryGetProperty("AutoDailyBackup", out var adb)) AutoDailyBackup = adb.GetBoolean();
                if (root.TryGetProperty("AutoDeleteBackupsOlderThan7Days", out var adb7)) AutoDeleteBackupsOlderThan7Days = adb7.GetBoolean();
                if (root.TryGetProperty("NearExpiryDays", out var ned)) NearExpiryDays = ned.GetInt32();
                if (root.TryGetProperty("DefaultGstRate", out var dgr)) DefaultGstRate = dgr.GetDecimal();
                if (root.TryGetProperty("BackupLocation", out var blp) && !string.IsNullOrWhiteSpace(blp.GetString()))
                {
                    BackupLocation = blp.GetString()!;
                }
                else
                {
                    BackupLocation = MedistockPaths.DefaultBackupsDirectory;
                }
            }
        }
        catch { }
    }

    public async Task PerformAutomatedBackupMaintenanceAsync()
    {
        if (_localBackupService == null) return;
        try
        {
            var targetFolder = string.IsNullOrWhiteSpace(BackupLocation) ? MedistockPaths.BackupsDirectory : BackupLocation.Trim();

            if (AutoDeleteBackupsOlderThan7Days)
            {
                _localBackupService.PruneOldBackups(targetFolder, maxBackupsToKeep: 7);
            }

            if (AutoDailyBackup)
            {
                await _localBackupService.CreateDailyBackupIfDueAsync(targetFolder);
            }

            RefreshBackupsList();
        }
        catch
        {
            // Non-blocking background maintenance
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        try
        {
            var targetBackupLocation = string.IsNullOrWhiteSpace(BackupLocation)
                ? MedistockPaths.DefaultBackupsDirectory
                : BackupLocation.Trim();

            var settingsObj = new
            {
                Theme = Theme,
                FontSize = FontSize,
                PharmacyName = PharmacyName,
                Gstin = Gstin,
                ContactPhone = ContactPhone,
                StoreAddress = StoreAddress,
                InvoicePrefix = InvoicePrefix,
                EnableBarcodeAudio = EnableBarcodeAudio,
                AutoDailyBackup = AutoDailyBackup,
                AutoDeleteBackupsOlderThan7Days = AutoDeleteBackupsOlderThan7Days,
                NearExpiryDays = NearExpiryDays,
                DefaultGstRate = DefaultGstRate,
                BackupLocation = targetBackupLocation,
                LastUpdated = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);

            ThemeChangedCallback?.Invoke(Theme);
            FontSizeChangedCallback?.Invoke(FontSize);

            StatusMessage = "Settings saved successfully! ✓";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to save settings: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task CreateLocalBackupAsync()
    {
        if (_localBackupService == null)
        {
            BackupStatusMessage = "Backup service is not available.";
            return;
        }

        try
        {
            IsBusy = true;
            BackupStatusMessage = "Creating local backup archive...";
            var targetDir = string.IsNullOrWhiteSpace(BackupLocation) ? MedistockPaths.BackupsDirectory : BackupLocation.Trim();
            var path = await _localBackupService.CreateBackupAsync(targetDir);
            BackupStatusMessage = $"✓ Backup saved: {Path.GetFileName(path)}";
            RefreshBackupsList();
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Backup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ResetBackupLocation()
    {
        BackupLocation = MedistockPaths.DefaultBackupsDirectory;
        RefreshBackupsList();
        StatusMessage = "Backup location reset to default. Click Save Settings to persist.";
    }

    [RelayCommand]
    public void OpenBackupFolder()
    {
        try
        {
            var target = string.IsNullOrWhiteSpace(BackupLocation) ? MedistockPaths.BackupsDirectory : BackupLocation.Trim();
            Directory.CreateDirectory(target);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task UploadCloudBackupAsync()
    {
        if (_cloudBackupService == null)
        {
            BackupStatusMessage = "Cloud backup service is not available.";
            return;
        }

        try
        {
            IsBusy = true;
            BackupStatusMessage = "Encrypting (AES-256) & uploading database snapshot...";
            var result = await _cloudBackupService.UploadBackupAsync();
            if (result.Success)
            {
                BackupStatusMessage = $"✓ Cloud backup uploaded! ID: {result.BackupId}";
                await RefreshCloudBackupsListAsync();
            }
            else
            {
                BackupStatusMessage = $"Cloud upload failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Cloud upload error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RestoreLocalBackupAsync(string? zipPath)
    {
        if (_localBackupService == null)
        {
            BackupStatusMessage = "Backup service is not available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            BackupStatusMessage = "Selected backup file does not exist.";
            return;
        }

        try
        {
            IsBusy = true;
            BackupStatusMessage = $"Restoring database from {Path.GetFileName(zipPath)}...";
            var result = await _localBackupService.RestoreFromBackupAsync(zipPath);
            if (result.Success)
            {
                BackupStatusMessage = $"✓ Database successfully restored from {Path.GetFileName(zipPath)}! (Please restart Medistock if you notice any cached data)";
                RefreshBackupsList();
            }
            else
            {
                BackupStatusMessage = $"Restore failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Restore error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RestoreCloudBackupAsync(string? backupId)
    {
        if (_cloudBackupService == null)
        {
            BackupStatusMessage = "Cloud backup service is not available.";
            return;
        }

        try
        {
            IsBusy = true;
            if (string.IsNullOrWhiteSpace(backupId))
            {
                var list = await _cloudBackupService.ListCloudBackupsAsync();
                if (list.Count == 0)
                {
                    BackupStatusMessage = "No cloud backups found on the server.";
                    return;
                }
                backupId = list[0].BackupId;
            }

            BackupStatusMessage = $"Downloading & decrypting cloud backup {backupId}...";
            var result = await _cloudBackupService.RestoreCloudBackupAsync(backupId);
            if (result.Success)
            {
                BackupStatusMessage = $"✓ Database successfully restored from cloud backup {backupId}! (Please restart Medistock if you notice any cached data)";
            }
            else
            {
                BackupStatusMessage = $"Cloud restore failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Cloud restore error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshCloudBackupsAsync()
    {
        try
        {
            IsBusy = true;
            BackupStatusMessage = "Querying server for available cloud backups...";
            await RefreshCloudBackupsListAsync();
            BackupStatusMessage = $"✓ Found {CloudBackups.Count} cloud backup(s) on the server.";
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Failed to refresh cloud backups: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        if (_updateService == null)
        {
            UpdateStatusMessage = "Update service not configured.";
            return;
        }

        try
        {
            IsBusy = true;
            UpdateStatusMessage = "Checking server for latest version...";
            var res = await _updateService.CheckForUpdateAsync();
            if (res != null && res.UpdateAvailable)
            {
                UpdateStatusMessage = $"✓ Update v{res.Version} available! Downloading in background...";
            }
            else
            {
                UpdateStatusMessage = "✓ You are using the latest version of Medistock.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Update check error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshBackupsList()
    {
        LocalBackups.Clear();
        if (_localBackupService != null)
        {
            var targetFolder = string.IsNullOrWhiteSpace(BackupLocation) ? MedistockPaths.BackupsDirectory : BackupLocation.Trim();
            foreach (var b in _localBackupService.ListLocalBackups(targetFolder))
            {
                LocalBackups.Add(b);
            }
        }
    }

    public async Task RefreshCloudBackupsListAsync()
    {
        CloudBackups.Clear();
        if (_cloudBackupService != null)
        {
            var list = await _cloudBackupService.ListCloudBackupsAsync();
            foreach (var b in list)
            {
                CloudBackups.Add(b);
            }
        }
    }

    public void RefreshLicenseInfo()
    {
        if (_activationService != null)
        {
            var token = _activationService.GetCurrentToken();
            if (token != null)
            {
                LicenseInfo = $"Org: {token.OrgName} | Plan: {token.Plan} | Exp: {token.ExpiresAt:yyyy-MM-dd}";
            }
            else
            {
                LicenseInfo = "Device Activated (Local Mode)";
            }
        }
    }

    public async Task RefreshProductCountAsync()
    {
        if (_medicineCatalogSeeder == null) return;
        try
        {
            ProductCount = await _medicineCatalogSeeder.GetProductCountAsync();
            HasProducts = ProductCount > 0;
        }
        catch { }
    }

    [RelayCommand]
    public async Task SeedMasterMedicinesAsync()
    {
        if (_medicineCatalogSeeder == null)
        {
            SeedStatusMessage = "Medicine catalog seeder service is not available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(MedicineJsonPath) || !File.Exists(MedicineJsonPath))
        {
            SeedStatusMessage = "Please specify a valid indian_medicine_data.json file path.";
            return;
        }

        try
        {
            IsSeeding = true;
            IsBusy = true;
            SeedProgressPercent = 0;
            SeedProgressStatus = "Preparing to import master medicines catalog...";
            SeedStatusMessage = "";

            var progress = new Progress<MedicineSeedProgress>(p =>
            {
                SeedProgressPercent = p.Percentage;
                SeedProgressStatus = p.Message;
            });

            var count = await _medicineCatalogSeeder.SeedFromJsonFileAsync(MedicineJsonPath, progress);
            await RefreshProductCountAsync();
            SeedStatusMessage = $"✓ Successfully seeded {count:N0} medicines into your catalog!";
        }
        catch (Exception ex)
        {
            SeedStatusMessage = $"Seeding failed: {ex.Message}";
        }
        finally
        {
            IsSeeding = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SeedSampleMedicinesAsync()
    {
        if (_medicineCatalogSeeder == null)
        {
            SeedStatusMessage = "Medicine catalog seeder service is not available.";
            return;
        }

        try
        {
            IsSeeding = true;
            IsBusy = true;
            SeedProgressPercent = 0;
            SeedProgressStatus = "Seeding starter sample pack (220 items with batches & stock)...";
            SeedStatusMessage = "";

            var progress = new Progress<MedicineSeedProgress>(p =>
            {
                SeedProgressPercent = p.Percentage;
                SeedProgressStatus = p.Message;
            });

            var count = await _medicineCatalogSeeder.SeedSampleStarterPackAsync(progress);
            await RefreshProductCountAsync();
            SeedStatusMessage = $"✓ Successfully seeded starter pack of {count} products with active batches and stock!";
        }
        catch (Exception ex)
        {
            SeedStatusMessage = $"Sample seeding failed: {ex.Message}";
        }
        finally
        {
            IsSeeding = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ClearMedicinesCatalogAsync()
    {
        if (_medicineCatalogSeeder == null) return;

        try
        {
            IsBusy = true;
            SeedStatusMessage = "Clearing all products and batches from catalog...";
            var deleted = await _medicineCatalogSeeder.ClearAllProductsAsync();
            await RefreshProductCountAsync();
            SeedStatusMessage = $"✓ Cleared {deleted:N0} products from database catalog.";
        }
        catch (Exception ex)
        {
            SeedStatusMessage = $"Clear catalog failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetMedicineJsonPath(string path)
    {
        MedicineJsonPath = path;
        IsJsonFileDetected = File.Exists(path);
    }

    partial void OnThemeChanged(string value)
    {
        ThemeChangedCallback?.Invoke(value);
    }

    partial void OnFontSizeChanged(string value)
    {
        FontSizeChangedCallback?.Invoke(value);
    }
}



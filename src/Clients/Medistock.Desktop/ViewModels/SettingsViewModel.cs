using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Medistock.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Medistock");
    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "user_settings.json");

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
    private string _statusMessage = "Settings loaded.";

    public Action<string>? ThemeChangedCallback { get; set; }
    public Action<string>? FontSizeChangedCallback { get; set; }

    public SettingsViewModel()
    {
        LoadSettings();
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
            }
        }
        catch { }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

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

    partial void OnThemeChanged(string value)
    {
        ThemeChangedCallback?.Invoke(value);
    }

    partial void OnFontSizeChanged(string value)
    {
        FontSizeChangedCallback?.Invoke(value);
    }
}

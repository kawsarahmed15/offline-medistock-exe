using System;
using System.IO;
using System.Text.Json;
using Medistock.Desktop.Views.Accounting;
using Medistock.Desktop.Views.Compliance;
using Medistock.Desktop.Views.Inventory;
using Medistock.Desktop.Views.POS;
using Medistock.Desktop.Views.Purchases;
using Medistock.Desktop.Views.Sales;
using Medistock.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Medistock.Desktop;

public sealed partial class MainWindow : Window
{
    private string _selectedThemeChoice = "Dark";
    // Use MedistockPaths so settings survive reinstalls and are isolated per Windows user
    private static readonly string SettingsFilePath = MedistockPaths.SettingsFile;


    public MainWindow()
    {
        this.InitializeComponent();

        Title = "Medistock — Pharmacy ERP & POS";

        // Maximize window by default on launch & set App Icon
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }

                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            }
        }
        catch { }

        // Check if settings exist (Theme & FontSize)
        InitializePreferences();

        ContentFrame.Navigated += (s, e) =>
        {
            if (ContentFrame.Content is DependencyObject depObj)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    Services.TypographyService.ScaleElementTree(depObj, Services.TypographyService.CurrentScale);
                });
            }
        };

        // Select POS as default active module
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void InitializePreferences()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("FontSize", out var fontProp))
                {
                    var savedFontSize = fontProp.GetString();
                    var baseSize = Services.TypographyService.ParseFontSize(savedFontSize);
                    ApplyFontSize(baseSize);
                }

                if (doc.RootElement.TryGetProperty("Theme", out var themeProp))
                {
                    var savedTheme = themeProp.GetString();
                    ApplyTheme(savedTheme == "Light" ? ElementTheme.Light : ElementTheme.Dark);
                    ThemeOnboardingOverlay.Visibility = Visibility.Collapsed;
                    return;
                }
            }
        }
        catch { }

        // First Launch: Default to Dark preview and show Onboarding modal
        _selectedThemeChoice = "Dark";
        ApplyTheme(ElementTheme.Dark);
        UpdateThemeCardHighlights("Dark");
        ThemeOnboardingOverlay.Visibility = Visibility.Visible;
    }

    private void ApplyTheme(ElementTheme theme)
    {
        if (RootGrid != null)
        {
            RootGrid.RequestedTheme = theme;
        }

        if (ThemeToggleButton != null)
        {
            ThemeToggleButton.Content = theme == ElementTheme.Dark ? "🌙 Dark" : "☀️ Light";
        }
    }

    private void ApplyFontSize(double baseFontSize)
    {
        if (RootGrid != null)
        {
            Services.TypographyService.ApplyFontScaling(RootGrid, baseFontSize);
        }

        if (ContentFrame.Content is DependencyObject depObj)
        {
            Services.TypographyService.ScaleElementTree(depObj, Services.TypographyService.CurrentScale);
        }
    }

    private void SaveThemePreference(string themeName)
    {
        try
        {
            // Directory is guaranteed by MedistockPaths.EnsureAllDirectoriesExist() at startup
            string currentFontSize = "Standard (13px)";
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var existingJson = File.ReadAllText(SettingsFilePath);
                    using var doc = JsonDocument.Parse(existingJson);
                    if (doc.RootElement.TryGetProperty("FontSize", out var fp))
                    {
                        currentFontSize = fp.GetString() ?? currentFontSize;
                    }
                }
                catch { }
            }

            var settingsObj = new
            {
                Theme = themeName,
                FontSize = currentFontSize,
                ConfiguredAt = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }

    private void SelectDarkTheme_Click(object sender, RoutedEventArgs e)
    {
        _selectedThemeChoice = "Dark";
        ApplyTheme(ElementTheme.Dark);
        UpdateThemeCardHighlights("Dark");
    }

    private void SelectLightTheme_Click(object sender, RoutedEventArgs e)
    {
        _selectedThemeChoice = "Light";
        ApplyTheme(ElementTheme.Light);
        UpdateThemeCardHighlights("Light");
    }

    private void UpdateThemeCardHighlights(string theme)
    {
        if (DarkThemeCard == null || LightThemeCard == null) return;

        if (theme == "Dark")
        {
            DarkThemeCard.BorderThickness = new Thickness(2);
            LightThemeCard.BorderThickness = new Thickness(1);
        }
        else
        {
            DarkThemeCard.BorderThickness = new Thickness(1);
            LightThemeCard.BorderThickness = new Thickness(2);
        }
    }

    private void ConfirmThemeSelection_Click(object sender, RoutedEventArgs e)
    {
        SaveThemePreference(_selectedThemeChoice);
        ThemeOnboardingOverlay.Visibility = Visibility.Collapsed;
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var currentTheme = RootGrid.RequestedTheme == ElementTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
        var newTheme = currentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        var newThemeStr = newTheme == ElementTheme.Dark ? "Dark" : "Light";

        ApplyTheme(newTheme);
        SaveThemePreference(newThemeStr);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "pos":
                    ContentFrame.Content = App.Services.GetRequiredService<PosPage>();
                    break;
                case "inventory":
                    ContentFrame.Content = App.Services.GetRequiredService<InventoryPage>();
                    break;
                case "expiry":
                    ContentFrame.Content = App.Services.GetRequiredService<ExpiryDashboardPage>();
                    break;
                case "compliance":
                    ContentFrame.Content = App.Services.GetRequiredService<ScheduleRegisterPage>();
                    break;
                case "purchases":
                    ContentFrame.Content = App.Services.GetRequiredService<PurchaseEntryPage>();
                    break;
                case "accounting":
                    ContentFrame.Content = App.Services.GetRequiredService<AccountingPage>();
                    break;
                case "sales_history":
                    ContentFrame.Content = App.Services.GetRequiredService<SalesHistoryPage>();
                    break;
                case "gst_reports":
                    ContentFrame.Content = App.Services.GetRequiredService<GstReportsPage>();
                    break;
                case "stock_transfers":
                    ContentFrame.Content = App.Services.GetRequiredService<StockTransfersPage>();
                    break;
                case "b2b_commerce":
                    ContentFrame.Content = App.Services.GetRequiredService<Views.B2B.B2bCommercePage>();
                    break;
                case "bill_customizer":
                    ContentFrame.Content = App.Services.GetRequiredService<Views.Settings.BillCustomizerPage>();
                    break;
                case "settings":
                    var settingsPage = App.Services.GetRequiredService<Views.Settings.SettingsPage>();
                    settingsPage.ViewModel.ThemeChangedCallback = (newTheme) =>
                    {
                        ApplyTheme(newTheme == "Light" ? ElementTheme.Light : ElementTheme.Dark);
                    };
                    settingsPage.ViewModel.FontSizeChangedCallback = (newFontSize) =>
                    {
                        var baseSize = Services.TypographyService.ParseFontSize(newFontSize);
                        ApplyFontSize(baseSize);
                    };
                    ContentFrame.Content = settingsPage;
                    break;
            }
        }
    }
}

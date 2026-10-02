using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Medistock.Infrastructure.Data;

namespace Medistock.Desktop.Services;

public static class ThemeService
{
    private static ElementTheme? _currentTheme;

    public static event EventHandler<ElementTheme>? ThemeChanged;

    public static ElementTheme CurrentTheme
    {
        get
        {
            if (_currentTheme.HasValue && _currentTheme.Value != ElementTheme.Default)
                return _currentTheme.Value;

            try
            {
                if (File.Exists(MedistockPaths.SettingsFile))
                {
                    var json = File.ReadAllText(MedistockPaths.SettingsFile);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Theme", out var tp))
                    {
                        var theme = string.Equals(tp.GetString(), "Light", StringComparison.OrdinalIgnoreCase)
                            ? ElementTheme.Light
                            : ElementTheme.Dark;
                        _currentTheme = theme;
                        return theme;
                    }
                }
            }
            catch { }

            return ElementTheme.Dark;
        }
        set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ThemeChanged?.Invoke(null, value);
            }
        }
    }

    public static bool IsLightTheme => CurrentTheme == ElementTheme.Light;
}

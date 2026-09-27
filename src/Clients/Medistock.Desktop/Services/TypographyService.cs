using System;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Medistock.Desktop.Services;

public static class TypographyService
{
    public static readonly DependencyProperty OriginalFontSizeProperty =
        DependencyProperty.RegisterAttached(
            "OriginalFontSize",
            typeof(double),
            typeof(TypographyService),
            new PropertyMetadata(0.0));

    public static double GetOriginalFontSize(DependencyObject obj) => (double)obj.GetValue(OriginalFontSizeProperty);
    public static void SetOriginalFontSize(DependencyObject obj, double value) => obj.SetValue(OriginalFontSizeProperty, value);

    public const double StandardBaseFontSize = 13.0;

    private static double _currentScale = 1.0;
    private static double _currentBaseFontSize = 13.0;

    public static double CurrentScale => _currentScale;
    public static double CurrentBaseFontSize => _currentBaseFontSize;

    public static double ParseFontSize(string? fontSizeSetting)
    {
        if (string.IsNullOrWhiteSpace(fontSizeSetting)) return StandardBaseFontSize;

        var match = Regex.Match(fontSizeSetting, @"(\d+(\.\d+)?)");
        if (match.Success && double.TryParse(match.Groups[1].Value, out var val))
        {
            if (val >= 9 && val <= 30)
            {
                return val;
            }
        }

        if (fontSizeSetting.Contains("Compact", StringComparison.OrdinalIgnoreCase)) return 11.0;
        if (fontSizeSetting.Contains("Standard", StringComparison.OrdinalIgnoreCase)) return 13.0;
        if (fontSizeSetting.Contains("Large", StringComparison.OrdinalIgnoreCase)) return 15.0;
        if (fontSizeSetting.Contains("Extra", StringComparison.OrdinalIgnoreCase)) return 17.0;
        if (fontSizeSetting.Contains("Huge", StringComparison.OrdinalIgnoreCase)) return 19.0;

        return StandardBaseFontSize;
    }

    public static void ApplyFontScaling(FrameworkElement root, double targetBaseFontSize)
    {
        _currentBaseFontSize = targetBaseFontSize;
        _currentScale = targetBaseFontSize / StandardBaseFontSize;

        UpdateApplicationResourceTokens(_currentScale, targetBaseFontSize);

        if (root != null)
        {
            ScaleElementTree(root, _currentScale);
        }
    }

    public static void UpdateApplicationResourceTokens(double scale, double baseFontSize)
    {
        try
        {
            var res = Microsoft.UI.Xaml.Application.Current?.Resources;
            if (res == null) return;

            res["FontSize10"] = Math.Round(10.0 * scale, 1);
            res["FontSize11"] = Math.Round(11.0 * scale, 1);
            res["FontSize12"] = Math.Round(12.0 * scale, 1);
            res["FontSize13"] = Math.Round(13.0 * scale, 1);
            res["FontSize14"] = Math.Round(14.0 * scale, 1);
            res["FontSize15"] = Math.Round(15.0 * scale, 1);
            res["FontSize16"] = Math.Round(16.0 * scale, 1);
            res["FontSize18"] = Math.Round(18.0 * scale, 1);
            res["FontSize20"] = Math.Round(20.0 * scale, 1);
            res["FontSize24"] = Math.Round(24.0 * scale, 1);
            res["FontSize28"] = Math.Round(28.0 * scale, 1);
            res["FontSize32"] = Math.Round(32.0 * scale, 1);

            res["ControlContentThemeFontSize"] = baseFontSize;
            res["BodyTextBlockFontSize"] = baseFontSize;
            res["CaptionTextBlockFontSize"] = Math.Round(12.0 * scale, 1);
            res["SubtitleTextBlockFontSize"] = Math.Round(20.0 * scale, 1);
            res["TitleTextBlockFontSize"] = Math.Round(28.0 * scale, 1);
        }
        catch { }
    }

    public static void ScaleElementTree(DependencyObject? parent, double scale)
    {
        if (parent == null) return;

        if (parent is TextBlock tb)
        {
            var orig = GetOriginalFontSize(tb);
            if (orig <= 0)
            {
                orig = tb.FontSize;
                SetOriginalFontSize(tb, orig);
            }
            tb.FontSize = Math.Max(9.0, Math.Round(orig * scale, 1));
        }
        else if (parent is Control ctrl)
        {
            var orig = GetOriginalFontSize(ctrl);
            if (orig <= 0)
            {
                orig = ctrl.FontSize;
                SetOriginalFontSize(ctrl, orig);
            }
            ctrl.FontSize = Math.Max(9.0, Math.Round(orig * scale, 1));
        }

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            ScaleElementTree(child, scale);
        }
    }
}

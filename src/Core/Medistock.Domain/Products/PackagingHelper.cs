using System;

namespace Medistock.Domain.Products;

public record PackagingBreakdown(
    int StripsPerBox,
    int TabsPerStrip,
    int TotalUnitsPerBox,
    string FormattedDescription
);

public static class PackagingHelper
{
    public static PackagingBreakdown Parse(string? packText, string baseUnit = "TAB")
    {
        if (string.IsNullOrWhiteSpace(packText))
        {
            return new PackagingBreakdown(1, 10, 10, $"1x10 (10 {baseUnit})");
        }

        var unit = string.IsNullOrWhiteSpace(baseUnit) ? "TAB" : baseUnit.Trim().ToUpperInvariant();
        var text = packText.Trim();

        // Extract numbers separated by 'x' or '*' or 'X'
        var parts = text.Split(new[] { 'x', 'X', '*' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 3)
        {
            // e.g. 10x5x5 or 10x10x5
            if (int.TryParse(CleanNumber(parts[0]), out int b) &&
                int.TryParse(CleanNumber(parts[1]), out int s) &&
                int.TryParse(CleanNumber(parts[2]), out int u))
            {
                int stripsPerBox = Math.Max(1, b);
                int tabsPerStrip = Math.Max(1, s * u); // e.g. 5x5 = 25 tabs per strip
                int totalUnits = stripsPerBox * tabsPerStrip;
                return new PackagingBreakdown(stripsPerBox, tabsPerStrip, totalUnits, $"{b}x{s}x{u} ({totalUnits} {unit})");
            }
        }
        else if (parts.Length == 2)
        {
            // e.g. 10x10 or 10x15 or 1x10
            if (int.TryParse(CleanNumber(parts[0]), out int b) &&
                int.TryParse(CleanNumber(parts[1]), out int s))
            {
                int stripsPerBox = Math.Max(1, b);
                int tabsPerStrip = Math.Max(1, s);
                int totalUnits = stripsPerBox * tabsPerStrip;
                return new PackagingBreakdown(stripsPerBox, tabsPerStrip, totalUnits, $"{b}x{s} ({totalUnits} {unit})");
            }
        }
        else if (parts.Length == 1)
        {
            if (int.TryParse(CleanNumber(parts[0]), out int num) && num > 0)
            {
                int tabsPerStrip = (num <= 1 && (text.Contains("STRIP", StringComparison.OrdinalIgnoreCase) || unit is "TAB" or "CAP"))
                    ? 10
                    : num;
                return new PackagingBreakdown(1, tabsPerStrip, tabsPerStrip, $"{tabsPerStrip} {unit}/Strip");
            }
        }

        return new PackagingBreakdown(1, 10, 10, text);
    }

    private static string CleanNumber(string s)
    {
        var digits = new System.Text.StringBuilder();
        foreach (var c in s)
        {
            if (char.IsDigit(c)) digits.Append(c);
        }
        return digits.ToString();
    }
}

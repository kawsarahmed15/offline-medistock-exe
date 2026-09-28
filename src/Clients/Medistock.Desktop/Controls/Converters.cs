using System;
using Microsoft.UI.Xaml.Data;

namespace Medistock.Desktop.Controls;

public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string format && value != null)
        {
            return string.Format(format, value);
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw KinfeNotImplementedException();
    }

    private static NotImplementedException KinfeNotImplementedException() => new();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        if (value is int count)
        {
            return count > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        if (value is string s)
        {
            return !string.IsNullOrWhiteSpace(s) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        return value != null ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Microsoft.UI.Xaml.Visibility.Visible;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return !b ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        if (value is int count)
        {
            return count == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        if (value is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        return value == null ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is not Microsoft.UI.Xaml.Visibility.Visible;
    }
}

public class HexToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var cleanHex = hex.Trim().TrimStart('#');
                byte a = 255, r = 0, g = 0, b = 0;
                if (cleanHex.Length == 6)
                {
                    r = System.Convert.ToByte(cleanHex.Substring(0, 2), 16);
                    g = System.Convert.ToByte(cleanHex.Substring(2, 2), 16);
                    b = System.Convert.ToByte(cleanHex.Substring(4, 2), 16);
                }
                else if (cleanHex.Length == 8)
                {
                    a = System.Convert.ToByte(cleanHex.Substring(0, 2), 16);
                    r = System.Convert.ToByte(cleanHex.Substring(2, 2), 16);
                    g = System.Convert.ToByte(cleanHex.Substring(4, 2), 16);
                    b = System.Convert.ToByte(cleanHex.Substring(6, 2), 16);
                }
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            }
            catch
            {
            }
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class TabVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var currentTab = value?.ToString() ?? "Entry";
        var targetTab = parameter?.ToString() ?? "Entry";
        return string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase)
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class SelectedTabBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var currentTab = value?.ToString() ?? "Entry";
        var targetTab = parameter?.ToString() ?? "Entry";
        bool isSelected = string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase);

        if (isSelected)
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 5, 150, 105)); // Brand Green #059669
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class SelectedTabForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var currentTab = value?.ToString() ?? "Entry";
        var targetTab = parameter?.ToString() ?? "Entry";
        bool isSelected = string.Equals(currentTab, targetTab, StringComparison.OrdinalIgnoreCase);

        if (isSelected)
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184)); // Slate 400
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class CurrencyFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is decimal d) return $"₹{d:N2}";
        if (value is double db) return $"₹{db:N2}";
        if (value is float f) return $"₹{f:N2}";
        if (value is int i) return $"₹{i:N2}";
        return "₹0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class DateFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dt) return dt.ToString("dd-MM-yyyy");
        if (value is DateTimeOffset dto) return dto.ToString("dd-MM-yyyy");
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class MonthYearFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dt) return dt.ToString("MM/yyyy");
        if (value is DateTimeOffset dto) return dto.ToString("MM/yyyy");
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PostedStatusVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Medistock.Domain.Purchases.PurchaseInvoiceStatus s)
            return s == Medistock.Domain.Purchases.PurchaseInvoiceStatus.Posted ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        if (value is int intVal)
            return intVal == 1 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        return Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class StatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var msg = value as string ?? "";
        if (msg.StartsWith("❌") || msg.StartsWith("⚠️"))
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38)); // red
        if (msg.StartsWith("✅"))
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 5, 150, 105)); // green
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139)); // muted
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}


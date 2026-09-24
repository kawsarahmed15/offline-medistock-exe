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

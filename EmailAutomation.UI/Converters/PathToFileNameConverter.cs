using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace EmailAutomation.UI.Converters;

public class PathToFileNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string path && !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace EmailAutomation.UI.Converters;

/// <summary>
/// Maps a nullable bool "did the last test succeed" to a status color: green/red/neutral.
/// </summary>
public class BoolToStatusBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => Brushes.SeaGreen,
            false => Brushes.IndianRed,
            _ => null,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

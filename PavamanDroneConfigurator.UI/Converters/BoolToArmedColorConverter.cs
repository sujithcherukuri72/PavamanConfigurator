using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PavamanDroneConfigurator.UI.Converters;

/// <summary>
/// Converts armed status boolean to appropriate color.
/// Armed = Red, Disarmed = Green
/// </summary>
public class BoolToArmedColorConverter : IValueConverter
{
    public static readonly BoolToArmedColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isArmed)
        {
            // Armed = Red (#DC2626), Disarmed = Green (#16A34A)
            return isArmed 
                ? new SolidColorBrush(Color.Parse("#DC2626")) 
                : new SolidColorBrush(Color.Parse("#16A34A"));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToArmedTextConverter : IValueConverter
{
    public static readonly BoolToArmedTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isArmed)
        {
            return isArmed ? "Armed" : "Disarmed";
        }
        return "Unknown";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

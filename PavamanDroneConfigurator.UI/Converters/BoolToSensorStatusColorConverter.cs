using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PavamanDroneConfigurator.UI.Converters;

/// <summary>
/// Converts a boolean to a color for sensor availability status.
/// True (available) = Green (#10B981), False (unavailable) = Red (#EF4444)
/// </summary>
public class BoolToSensorStatusColorConverter : IValueConverter
{
    public static readonly BoolToSensorStatusColorConverter Instance = new();
    
    private static readonly SolidColorBrush AvailableBrush = new(Color.Parse("#10B981"));
    private static readonly SolidColorBrush UnavailableBrush = new(Color.Parse("#EF4444"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAvailable)
        {
            return isAvailable ? AvailableBrush : UnavailableBrush;
        }
        return UnavailableBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

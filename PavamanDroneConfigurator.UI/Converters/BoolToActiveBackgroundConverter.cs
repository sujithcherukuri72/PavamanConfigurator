using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PavamanDroneConfigurator.UI.Converters;

/// <summary>
/// Converts a boolean value to a background brush for active/inactive state.
/// Active: Light blue (#E7F3FF), Inactive: Light gray (#F8FAFC)
/// </summary>
public class BoolToActiveBackgroundConverter : IValueConverter
{
    public static readonly BoolToActiveBackgroundConverter Instance = new();
    
    private static readonly SolidColorBrush ActiveBrush = new(Color.Parse("#E7F3FF"));
    private static readonly SolidColorBrush InactiveBrush = new(Color.Parse("#F8FAFC"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? ActiveBrush : InactiveBrush;
        }
        return InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

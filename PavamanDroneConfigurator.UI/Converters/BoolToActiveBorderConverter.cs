using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PavamanDroneConfigurator.UI.Converters;

/// <summary>
/// Converts a boolean value to a border brush for active/inactive state.
/// Active: Blue (#0B5ED7), Inactive: Light gray (#E2E8F0)
/// </summary>
public class BoolToActiveBorderConverter : IValueConverter
{
    public static readonly BoolToActiveBorderConverter Instance = new();
    
    private static readonly SolidColorBrush ActiveBrush = new(Color.Parse("#0B5ED7"));
    private static readonly SolidColorBrush InactiveBrush = new(Color.Parse("#E2E8F0"));

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

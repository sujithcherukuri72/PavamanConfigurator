using System.Text.Json.Serialization;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Represents ArduPilot parameter metadata loaded from official JSON file.
/// Provides comprehensive information about each parameter including description,
/// range, units, default values, and enumeration options.
/// </summary>
public class ArduPilotParameterMetadata
{
    /// <summary>
    /// The full parameter name (e.g., "ACRO_BAL_PITCH")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The parameter group/category (e.g., "ACRO_", "ATC_")
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the parameter
    /// </summary>
    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Detailed description of the parameter's purpose and usage
    /// </summary>
    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    /// <summary>
    /// Units for the parameter value (e.g., "m", "deg", "Hz", "%")
    /// </summary>
    [JsonPropertyName("Units")]
    public string? Units { get; set; }

    /// <summary>
    /// User type classification (e.g., "Standard", "Advanced")
    /// </summary>
    [JsonPropertyName("User")]
    public string? User { get; set; }

    /// <summary>
    /// Range constraints for the parameter value
    /// </summary>
    [JsonPropertyName("Range")]
    public ParameterRange? Range { get; set; }

    /// <summary>
    /// Dictionary of valid values and their descriptions for enum-type parameters
    /// Key is the numeric value, Value is the description
    /// </summary>
    [JsonPropertyName("Values")]
    public Dictionary<string, string>? Values { get; set; }

    /// <summary>
    /// Bitmask field descriptions for bitmask-type parameters
    /// Key is the bit position, Value is the description
    /// </summary>
    [JsonPropertyName("Bitmask")]
    public Dictionary<string, string>? Bitmask { get; set; }

    /// <summary>
    /// Increment step for the parameter value
    /// </summary>
    [JsonPropertyName("Increment")]
    public string? Increment { get; set; }

    /// <summary>
    /// Whether this parameter requires a reboot to take effect
    /// </summary>
    [JsonPropertyName("RebootRequired")]
    public string? RebootRequired { get; set; }

    /// <summary>
    /// Whether this parameter is read-only
    /// </summary>
    [JsonPropertyName("ReadOnly")]
    public string? ReadOnly { get; set; }

    /// <summary>
    /// Whether this parameter is volatile (can change during operation)
    /// </summary>
    [JsonPropertyName("Volatile")]
    public string? Volatile { get; set; }

    /// <summary>
    /// Path or location information for the parameter
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    // Computed properties for UI display

    /// <summary>
    /// Gets the display name or falls back to the parameter name
    /// </summary>
    public string DisplayNameOrName => !string.IsNullOrEmpty(DisplayName) ? DisplayName : Name;

    /// <summary>
    /// Gets a formatted string showing the valid range
    /// </summary>
    public string RangeDisplay
    {
        get
        {
            if (Range != null)
            {
                var low = Range.Low ?? "?";
                var high = Range.High ?? "?";
                return $"{low} to {high}";
            }
            return "Not specified";
        }
    }

    /// <summary>
    /// Gets the minimum value as a float, if available
    /// </summary>
    public float? MinValue
    {
        get
        {
            if (Range?.Low != null && float.TryParse(Range.Low, out var min))
                return min;
            return null;
        }
    }

    /// <summary>
    /// Gets the maximum value as a float, if available
    /// </summary>
    public float? MaxValue
    {
        get
        {
            if (Range?.High != null && float.TryParse(Range.High, out var max))
                return max;
            return null;
        }
    }

    /// <summary>
    /// Gets the increment as a float, if available
    /// </summary>
    public float? IncrementValue
    {
        get
        {
            if (!string.IsNullOrEmpty(Increment) && float.TryParse(Increment, out var inc))
                return inc;
            return null;
        }
    }

    /// <summary>
    /// Indicates if this parameter has enum values
    /// </summary>
    public bool HasEnumValues => Values != null && Values.Count > 0;

    /// <summary>
    /// Indicates if this parameter has bitmask values
    /// </summary>
    public bool HasBitmaskValues => Bitmask != null && Bitmask.Count > 0;

    /// <summary>
    /// Indicates if a reboot is required after changing this parameter
    /// </summary>
    public bool IsRebootRequired => 
        !string.IsNullOrEmpty(RebootRequired) && 
        RebootRequired.Equals("True", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Indicates if this parameter is read-only
    /// </summary>
    public bool IsReadOnly => 
        !string.IsNullOrEmpty(ReadOnly) && 
        ReadOnly.Equals("True", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a short description (first sentence) for display in compact views
    /// </summary>
    public string ShortDescription
    {
        get
        {
            if (string.IsNullOrEmpty(Description))
                return "No description available";

            var firstPeriod = Description.IndexOf('.');
            if (firstPeriod > 0 && firstPeriod < 150)
                return Description.Substring(0, firstPeriod + 1);

            if (Description.Length <= 150)
                return Description;

            return Description.Substring(0, 147) + "...";
        }
    }
}

/// <summary>
/// Represents the range constraints for a parameter
/// </summary>
public class ParameterRange
{
    [JsonPropertyName("low")]
    public string? Low { get; set; }

    [JsonPropertyName("high")]
    public string? High { get; set; }
}

/// <summary>
/// Represents a single enum option for display in the UI
/// </summary>
public class ParameterEnumOption
{
    public int Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Display => $"{Value}: {Label}";
}

/// <summary>
/// Represents a single bitmask option for display in the UI
/// </summary>
public class ParameterBitmaskOption
{
    public int BitPosition { get; set; }
    public int BitValue => 1 << BitPosition;
    public string Label { get; set; } = string.Empty;
    public bool IsSet { get; set; }
    public string Display => $"Bit {BitPosition}: {Label}";
}

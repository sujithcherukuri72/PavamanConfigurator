namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Contains metadata about a parameter from ArduPilot documentation.
/// Similar to Mission Planner's parameter metadata system.
/// </summary>
public class ParameterMetadata
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Units { get; set; }
    public float? MinValue { get; set; }
    public float? MaxValue { get; set; }
    public float? DefaultValue { get; set; }
    public float? Increment { get; set; }
    public string? Range { get; set; }
    public bool ReadOnly { get; set; }
    public bool RebootRequired { get; set; }
    public string? Group { get; set; }
    public Dictionary<int, string>? Values { get; set; } // For enum parameters
    public string? Bitmask { get; set; } // For bitmask parameters
}

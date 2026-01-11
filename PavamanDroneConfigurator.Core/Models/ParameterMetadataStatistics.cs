using System.Collections.Generic;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Statistics about the parameter metadata repository.
/// Used for displaying metadata overview in UI.
/// </summary>
public class ParameterMetadataStatistics
{
    /// <summary>
    /// Total number of parameters in the metadata repository.
    /// </summary>
    public int TotalParameters { get; set; }

    /// <summary>
    /// Number of parameters that have predefined option values (enum parameters).
    /// </summary>
    public int ParametersWithOptions { get; set; }

    /// <summary>
    /// Number of parameters that have min/max range constraints.
    /// </summary>
    public int ParametersWithRanges { get; set; }

    /// <summary>
    /// Total number of parameter groups/categories.
    /// </summary>
    public int TotalGroups { get; set; }

    /// <summary>
    /// List of all parameter group names.
    /// </summary>
    public List<string> GroupNames { get; set; } = new();
}

using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service for providing parameter metadata like Mission Planner.
/// </summary>
public interface IParameterMetadataService
{
    /// <summary>
    /// Gets metadata for a specific parameter.
    /// </summary>
    ParameterMetadata? GetMetadata(string parameterName);

    /// <summary>
    /// Gets all available parameter metadata.
    /// </summary>
    IEnumerable<ParameterMetadata> GetAllMetadata();

    /// <summary>
    /// Gets parameters in a specific group/category.
    /// </summary>
    IEnumerable<ParameterMetadata> GetParametersByGroup(string group);

    /// <summary>
    /// Gets all parameter groups/categories.
    /// </summary>
    IEnumerable<string> GetGroups();

    /// <summary>
    /// Updates a DroneParameter with metadata (description, range, etc).
    /// </summary>
    void EnrichParameter(DroneParameter parameter);
}

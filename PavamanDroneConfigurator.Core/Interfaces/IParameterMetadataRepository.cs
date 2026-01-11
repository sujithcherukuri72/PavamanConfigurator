using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Repository interface for parameter metadata storage and retrieval.
/// Follows Repository pattern for clean MVVM architecture.
/// Supports loading from ArduPilot XML files for Copter and Plane.
/// </summary>
public interface IParameterMetadataRepository
{
    /// <summary>
    /// Loads parameter metadata for specified vehicle type from ArduPilot XML.
    /// Downloads from GitHub or uses cached version.
    /// </summary>
    Task LoadMetadataAsync(VehicleType vehicleType);

    /// <summary>
    /// Gets metadata for a specific parameter by name.
    /// </summary>
    ParameterMetadata? GetByName(string parameterName);

    /// <summary>
    /// Gets all available parameter metadata.
    /// </summary>
    IEnumerable<ParameterMetadata> GetAll();

    /// <summary>
    /// Gets all parameters belonging to a specific group.
    /// </summary>
    IEnumerable<ParameterMetadata> GetByGroup(string group);

    /// <summary>
    /// Gets all distinct parameter groups.
    /// </summary>
    IEnumerable<string> GetAllGroups();

    /// <summary>
    /// Checks if metadata exists for a parameter.
    /// </summary>
    bool Exists(string parameterName);

    /// <summary>
    /// Gets the total count of parameters in the repository.
    /// </summary>
    int GetCount();
}

using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Interface for loading ArduPilot parameter metadata from official JSON files.
/// Provides async methods to load, search, and filter parameter metadata.
/// </summary>
public interface IArduPilotMetadataLoader
{
    /// <summary>
    /// Loads all parameter metadata from the JSON file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all parameter metadata</returns>
    Task<IReadOnlyList<ArduPilotParameterMetadata>> LoadAllMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for a specific parameter by name.
    /// </summary>
    /// <param name="parameterName">The parameter name to look up</param>
    /// <returns>The metadata if found, null otherwise</returns>
    ArduPilotParameterMetadata? GetMetadata(string parameterName);

    /// <summary>
    /// Searches for parameters matching the search text in name or description.
    /// </summary>
    /// <param name="searchText">Text to search for</param>
    /// <returns>Collection of matching parameter metadata</returns>
    IEnumerable<ArduPilotParameterMetadata> Search(string searchText);

    /// <summary>
    /// Gets all available parameter groups/categories.
    /// </summary>
    /// <returns>Collection of group names</returns>
    IEnumerable<string> GetGroups();

    /// <summary>
    /// Gets all parameters in a specific group.
    /// </summary>
    /// <param name="groupName">The group name to filter by</param>
    /// <returns>Collection of parameter metadata in the group</returns>
    IEnumerable<ArduPilotParameterMetadata> GetByGroup(string groupName);

    /// <summary>
    /// Indicates if the metadata has been loaded.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Gets the total number of parameters loaded.
    /// </summary>
    int TotalParameters { get; }

    /// <summary>
    /// Gets the file path from which metadata was loaded.
    /// </summary>
    string? LoadedFilePath { get; }

    /// <summary>
    /// Reloads metadata from the file, refreshing all cached data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the loaded metadata.
    /// </summary>
    ArduPilotMetadataStatistics GetStatistics();
}

/// <summary>
/// Statistics about the loaded ArduPilot parameter metadata.
/// </summary>
public record ArduPilotMetadataStatistics
{
    public int TotalParameters { get; init; }
    public int TotalGroups { get; init; }
    public int ParametersWithRanges { get; init; }
    public int ParametersWithEnums { get; init; }
    public int ParametersWithBitmasks { get; init; }
    public int ParametersRequiringReboot { get; init; }
    public int ReadOnlyParameters { get; init; }
    public DateTime? LoadedAt { get; init; }
}

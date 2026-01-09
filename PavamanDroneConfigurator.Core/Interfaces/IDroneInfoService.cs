using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for retrieving drone identification and version information.
/// </summary>
public interface IDroneInfoService
{
    /// <summary>
    /// Gets the current drone information.
    /// </summary>
    /// <returns>DroneInfo object with current drone details, or null if not connected.</returns>
    Task<DroneInfo?> GetDroneInfoAsync();

    /// <summary>
    /// Refreshes drone information from the connected vehicle.
    /// </summary>
    Task RefreshDroneInfoAsync();

    /// <summary>
    /// Event raised when drone information is updated.
    /// </summary>
    event EventHandler<DroneInfo>? DroneInfoUpdated;

    /// <summary>
    /// Gets whether drone info is currently available.
    /// </summary>
    bool IsInfoAvailable { get; }
}

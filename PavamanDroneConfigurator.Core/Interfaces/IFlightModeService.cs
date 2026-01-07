using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for managing flight mode configuration.
/// Handles reading/writing FLTMODE parameters via MAVLink.
/// </summary>
public interface IFlightModeService
{
    /// <summary>
    /// Event raised when flight mode settings are updated from the vehicle
    /// </summary>
    event EventHandler<FlightModeSettings>? FlightModeSettingsChanged;

    /// <summary>
    /// Event raised when current flight mode changes
    /// </summary>
    event EventHandler<FlightMode>? CurrentModeChanged;

    /// <summary>
    /// Event raised when the mode channel PWM value changes
    /// </summary>
    event EventHandler<int>? ModeChannelPwmChanged;

    /// <summary>
    /// Get the current flight mode settings from the vehicle
    /// </summary>
    Task<FlightModeSettings?> GetFlightModeSettingsAsync();

    /// <summary>
    /// Update all flight mode settings on the vehicle
    /// </summary>
    Task<bool> UpdateFlightModeSettingsAsync(FlightModeSettings settings);

    /// <summary>
    /// Set a specific flight mode slot (1-6)
    /// </summary>
    Task<bool> SetFlightModeAsync(int slot, FlightMode mode);

    /// <summary>
    /// Set the flight mode channel
    /// </summary>
    Task<bool> SetFlightModeChannelAsync(FlightModeChannel channel);

    /// <summary>
    /// Set simple mode for a specific slot (1-6)
    /// </summary>
    Task<bool> SetSimpleModeAsync(int slot, SimpleMode simpleMode);

    /// <summary>
    /// Get the current active flight mode
    /// </summary>
    Task<FlightMode?> GetCurrentFlightModeAsync();

    /// <summary>
    /// Get available flight modes for the vehicle type
    /// </summary>
    IEnumerable<FlightModeInfo> GetAvailableFlightModes();

    /// <summary>
    /// Get recommended safe flight modes for beginners
    /// </summary>
    IEnumerable<FlightModeInfo> GetRecommendedFlightModes();

    /// <summary>
    /// Get GPS-required flight modes
    /// </summary>
    IEnumerable<FlightModeInfo> GetGpsRequiredModes();

    /// <summary>
    /// Apply default/recommended flight mode configuration
    /// </summary>
    Task<bool> ApplyDefaultConfigurationAsync();

    /// <summary>
    /// Validate flight mode configuration
    /// </summary>
    List<string> ValidateConfiguration(FlightModeSettings settings);
}

using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for managing drone safety settings with PDRL compliance.
/// </summary>
public interface ISafetyService
{
    /// <summary>
    /// Gets the current safety settings from the drone.
    /// </summary>
    Task<SafetySettings?> GetSafetySettingsAsync();
    
    /// <summary>
    /// Updates all safety settings on the drone.
    /// </summary>
    Task<bool> UpdateSafetySettingsAsync(SafetySettings settings);
    
    /// <summary>
    /// Validates safety settings against PDRL requirements.
    /// </summary>
    /// <returns>List of validation errors, empty if valid</returns>
    Task<List<string>> ValidatePDRLComplianceAsync(SafetySettings settings);
    
    /// <summary>
    /// Applies PDRL-recommended safety defaults.
    /// </summary>
    Task<SafetySettings> GetPDRLDefaultsAsync();
    
    /// <summary>
    /// Gets the current arming check configuration.
    /// </summary>
    Task<ArmingCheck> GetArmingChecksAsync();
    
    /// <summary>
    /// Sets arming check configuration.
    /// </summary>
    Task<bool> SetArmingChecksAsync(ArmingCheck checks);
    
    /// <summary>
    /// Gets current geofence settings.
    /// </summary>
    Task<(bool Enabled, FenceType Type, FenceAction Action, float AltMax, float Radius)> GetGeofenceSettingsAsync();
    
    /// <summary>
    /// Sets geofence settings.
    /// </summary>
    Task<bool> SetGeofenceSettingsAsync(bool enabled, FenceType type, FenceAction action, float altMax, float radius);
    
    /// <summary>
    /// Gets current RTL (Return To Launch) settings.
    /// </summary>
    Task<(float Altitude, float FinalAltitude, float LoiterTime, float Speed)> GetRTLSettingsAsync();
    
    /// <summary>
    /// Sets RTL settings.
    /// </summary>
    Task<bool> SetRTLSettingsAsync(float altitude, float finalAltitude, float loiterTime, float speed);
    
    /// <summary>
    /// Event fired when safety settings change.
    /// </summary>
    event EventHandler<SafetySettings>? SafetySettingsChanged;
    
    /// <summary>
    /// Event fired when a safety validation warning occurs.
    /// </summary>
    event EventHandler<string>? SafetyWarning;
}

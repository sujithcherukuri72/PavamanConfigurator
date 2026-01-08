using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for RC (Radio Control) calibration operations.
/// Handles RC channel calibration, monitoring, and attitude mapping via MAVLink.
/// </summary>
public interface IRcCalibrationService
{
    #region Events

    /// <summary>
    /// Fired when RC channel values are updated (from RC_CHANNELS MAVLink message)
    /// </summary>
    event EventHandler<RcChannelsUpdateEventArgs>? RcChannelsUpdated;

    /// <summary>
    /// Fired when calibration state changes
    /// </summary>
    event EventHandler<RcCalibrationProgress>? CalibrationStateChanged;

    /// <summary>
    /// Fired when calibration is completed
    /// </summary>
    event EventHandler<bool>? CalibrationCompleted;

    /// <summary>
    /// Fired when a configuration parameter is updated
    /// </summary>
    event EventHandler<string>? ParameterUpdated;

    #endregion

    #region Properties

    /// <summary>
    /// Current calibration state
    /// </summary>
    RcCalibrationState CurrentState { get; }

    /// <summary>
    /// Whether calibration is in progress
    /// </summary>
    bool IsCalibrating { get; }

    /// <summary>
    /// Current live RC channel values (1-16)
    /// </summary>
    IReadOnlyList<RcChannelValue> LiveChannelValues { get; }

    #endregion

    #region Channel Configuration

    /// <summary>
    /// Get complete RC calibration configuration from the vehicle
    /// </summary>
    Task<RcCalibrationConfiguration?> GetRcConfigurationAsync();

    /// <summary>
    /// Get configuration for a specific channel
    /// </summary>
    Task<RcChannelConfig?> GetChannelConfigAsync(RcChannel channel);

    /// <summary>
    /// Update configuration for a specific channel
    /// </summary>
    Task<bool> UpdateChannelConfigAsync(RcChannelConfig config);

    /// <summary>
    /// Update complete RC calibration configuration
    /// </summary>
    Task<bool> UpdateRcConfigurationAsync(RcCalibrationConfiguration config);

    #endregion

    #region Calibration Operations

    /// <summary>
    /// Start RC calibration process.
    /// User should move all sticks and switches to their extremes.
    /// </summary>
    Task<bool> StartCalibrationAsync();

    /// <summary>
    /// Stop/cancel RC calibration
    /// </summary>
    Task<bool> StopCalibrationAsync();

    /// <summary>
    /// Complete calibration and save values.
    /// Call this when user has finished moving sticks.
    /// </summary>
    Task<bool> CompleteCalibrationAsync();

    /// <summary>
    /// Reset calibration to defaults
    /// </summary>
    Task<bool> ResetCalibrationAsync();

    #endregion

    #region Attitude Channel Mapping

    /// <summary>
    /// Get current attitude channel mapping (RCMAP_ parameters)
    /// </summary>
    Task<AttitudeChannelMapping?> GetAttitudeMappingAsync();

    /// <summary>
    /// Update attitude channel mapping
    /// </summary>
    Task<bool> UpdateAttitudeMappingAsync(AttitudeChannelMapping mapping);

    /// <summary>
    /// Set roll channel mapping (RCMAP_ROLL)
    /// </summary>
    Task<bool> SetRollChannelAsync(RcChannel channel);

    /// <summary>
    /// Set pitch channel mapping (RCMAP_PITCH)
    /// </summary>
    Task<bool> SetPitchChannelAsync(RcChannel channel);

    /// <summary>
    /// Set throttle channel mapping (RCMAP_THROTTLE)
    /// </summary>
    Task<bool> SetThrottleChannelAsync(RcChannel channel);

    /// <summary>
    /// Set yaw channel mapping (RCMAP_YAW)
    /// </summary>
    Task<bool> SetYawChannelAsync(RcChannel channel);

    #endregion

    #region Channel Options

    /// <summary>
    /// Set RC channel option/function (RCx_OPTION)
    /// </summary>
    Task<bool> SetChannelOptionAsync(RcChannel channel, RcChannelOption option);

    /// <summary>
    /// Get RC channel option/function
    /// </summary>
    Task<RcChannelOption> GetChannelOptionAsync(RcChannel channel);

    /// <summary>
    /// Set channel reversed state (RCx_REVERSED)
    /// </summary>
    Task<bool> SetChannelReversedAsync(RcChannel channel, bool reversed);

    #endregion

    #region Validation

    /// <summary>
    /// Validate RC configuration for PDRL compliance
    /// </summary>
    List<string> ValidateConfiguration(RcCalibrationConfiguration config);

    /// <summary>
    /// Check if RC is calibrated and ready for flight
    /// </summary>
    Task<bool> IsRcCalibratedAsync();

    #endregion

    #region Defaults

    /// <summary>
    /// Apply PDRL-compliant default configuration
    /// </summary>
    Task<bool> ApplyPDRLDefaultsAsync();

    /// <summary>
    /// Get available RC channel options with descriptions
    /// </summary>
    IEnumerable<(RcChannelOption Option, string Label, string Description)> GetChannelOptions();

    #endregion
}

/// <summary>
/// Event args for RC channels update
/// </summary>
public class RcChannelsUpdateEventArgs : EventArgs
{
    /// <summary>
    /// All 16 channel values
    /// </summary>
    public IReadOnlyList<RcChannelValue> Channels { get; set; } = Array.Empty<RcChannelValue>();

    /// <summary>
    /// Number of active channels
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Signal strength (RSSI)
    /// </summary>
    public byte Rssi { get; set; }

    /// <summary>
    /// Get value for a specific channel
    /// </summary>
    public RcChannelValue? GetChannel(RcChannel channel)
    {
        var index = (int)channel - 1;
        return index >= 0 && index < Channels.Count ? Channels[index] : null;
    }

    /// <summary>
    /// Get value for a specific channel by index (1-16)
    /// </summary>
    public RcChannelValue? GetChannel(int channelIndex)
    {
        var index = channelIndex - 1;
        return index >= 0 && index < Channels.Count ? Channels[index] : null;
    }
}

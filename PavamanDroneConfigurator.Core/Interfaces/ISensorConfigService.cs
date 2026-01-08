using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for sensor configuration and extended calibration operations.
/// Handles compass detection, optical flow settings, and sensor status.
/// </summary>
public interface ISensorConfigService
{
    #region Events

    /// <summary>
    /// Fired when sensor configuration is updated
    /// </summary>
    event EventHandler<SensorCalibrationConfiguration>? ConfigurationChanged;

    /// <summary>
    /// Fired when a compass status changes
    /// </summary>
    event EventHandler<CompassInfo>? CompassStatusChanged;

    #endregion

    #region Compass Operations

    /// <summary>
    /// Get all detected compass sensors
    /// </summary>
    Task<List<CompassInfo>> GetCompassesAsync();

    /// <summary>
    /// Get compass info by index (1-3)
    /// </summary>
    Task<CompassInfo?> GetCompassAsync(int index);

    /// <summary>
    /// Enable or disable a compass (COMPASS_USEx)
    /// </summary>
    Task<bool> SetCompassEnabledAsync(int index, bool enabled);

    /// <summary>
    /// Set compass priority (reorder compasses)
    /// </summary>
    Task<bool> SetCompassPriorityAsync(int compassIndex, int newPriority);

    /// <summary>
    /// Check if compass calibration is required
    /// </summary>
    Task<bool> IsCompassCalibrationRequiredAsync(int index);

    /// <summary>
    /// Get compass calibration offsets
    /// </summary>
    Task<(float X, float Y, float Z)> GetCompassOffsetsAsync(int index);

    #endregion

    #region Accelerometer Operations

    /// <summary>
    /// Get accelerometer calibration info
    /// </summary>
    Task<AccelerometerInfo> GetAccelerometerInfoAsync();

    /// <summary>
    /// Check if accelerometer is calibrated
    /// </summary>
    Task<bool> IsAccelerometerCalibratedAsync();

    /// <summary>
    /// Get accelerometer offsets
    /// </summary>
    Task<(float X, float Y, float Z)> GetAccelOffsetsAsync();

    /// <summary>
    /// Get accelerometer scale factors
    /// </summary>
    Task<(float X, float Y, float Z)> GetAccelScalesAsync();

    #endregion

    #region Optical Flow Operations

    /// <summary>
    /// Get optical flow sensor settings
    /// </summary>
    Task<FlowSensorSettings?> GetFlowSettingsAsync();

    /// <summary>
    /// Update optical flow sensor settings
    /// </summary>
    Task<bool> UpdateFlowSettingsAsync(FlowSensorSettings settings);

    /// <summary>
    /// Enable or disable optical flow sensor
    /// </summary>
    Task<bool> SetFlowEnabledAsync(FlowType type);

    /// <summary>
    /// Set flow sensor X scale factor (FLOW_FXSCALER)
    /// </summary>
    Task<bool> SetFlowXScaleAsync(float scale);

    /// <summary>
    /// Set flow sensor Y scale factor (FLOW_FYSCALER)
    /// </summary>
    Task<bool> SetFlowYScaleAsync(float scale);

    /// <summary>
    /// Set flow sensor yaw alignment (FLOW_ORIENT_YAW)
    /// </summary>
    Task<bool> SetFlowYawAlignmentAsync(float degrees);

    #endregion

    #region Level Horizon Operations

    /// <summary>
    /// Check if level horizon is calibrated
    /// </summary>
    Task<bool> IsLevelCalibratedAsync();

    /// <summary>
    /// Get level trim values (AHRS_TRIM_X, AHRS_TRIM_Y, AHRS_TRIM_Z)
    /// </summary>
    Task<(float X, float Y, float Z)> GetLevelTrimsAsync();

    #endregion

    #region Barometer Operations

    /// <summary>
    /// Check if barometer is calibrated
    /// </summary>
    Task<bool> IsBarometerCalibratedAsync();

    /// <summary>
    /// Get ground pressure setting (GND_ABS_PRESS)
    /// </summary>
    Task<float> GetGroundPressureAsync();

    #endregion

    #region Complete Configuration

    /// <summary>
    /// Get complete sensor calibration configuration
    /// </summary>
    Task<SensorCalibrationConfiguration?> GetSensorConfigurationAsync();

    /// <summary>
    /// Validate sensor configuration for PDRL compliance
    /// </summary>
    List<string> ValidateConfiguration(SensorCalibrationConfiguration config);

    #endregion
}

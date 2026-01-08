using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for PID tuning operations.
/// Handles reading/writing ArduPilot ATC_ and tuning parameters via MAVLink.
/// </summary>
public interface IPidTuningService
{
    #region Events

    /// <summary>
    /// Fired when PID settings are updated from the vehicle
    /// </summary>
    event EventHandler<PidTuningConfiguration>? SettingsChanged;

    /// <summary>
    /// Fired when a specific parameter is updated
    /// </summary>
    event EventHandler<string>? ParameterUpdated;

    #endregion

    #region Basic Tuning

    /// <summary>
    /// Get basic tuning settings from the vehicle
    /// </summary>
    Task<BasicTuningSettings?> GetBasicTuningSettingsAsync();

    /// <summary>
    /// Update basic tuning settings on the vehicle
    /// </summary>
    Task<bool> UpdateBasicTuningSettingsAsync(BasicTuningSettings settings);

    /// <summary>
    /// Set RC feel (input time constant)
    /// Parameter: ATC_INPUT_TC
    /// </summary>
    Task<bool> SetRcFeelAsync(float value);

    /// <summary>
    /// Set roll/pitch sensitivity
    /// Affects ATC_ACCEL_R_MAX and ATC_ACCEL_P_MAX
    /// </summary>
    Task<bool> SetRollPitchSensitivityAsync(float value);

    /// <summary>
    /// Set climb sensitivity
    /// Parameter: PILOT_ACCEL_Z
    /// </summary>
    Task<bool> SetClimbSensitivityAsync(float value);

    /// <summary>
    /// Set motor spin when armed
    /// Parameter: MOT_SPIN_ARM
    /// </summary>
    Task<bool> SetSpinWhileArmedAsync(float value);

    #endregion

    #region Advanced Tuning (Per-Axis PID)

    /// <summary>
    /// Get PID settings for a specific axis
    /// </summary>
    Task<AxisPidSettings?> GetAxisPidSettingsAsync(TuningAxis axis);

    /// <summary>
    /// Update all PID settings for a specific axis
    /// </summary>
    Task<bool> UpdateAxisPidSettingsAsync(AxisPidSettings settings);

    /// <summary>
    /// Set angle P gain for an axis
    /// Parameters: ATC_ANG_RLL_P, ATC_ANG_PIT_P, ATC_ANG_YAW_P
    /// </summary>
    Task<bool> SetAnglePAsync(TuningAxis axis, float value);

    /// <summary>
    /// Set rate P gain for an axis
    /// Parameters: ATC_RAT_RLL_P, ATC_RAT_PIT_P, ATC_RAT_YAW_P
    /// </summary>
    Task<bool> SetRatePAsync(TuningAxis axis, float value);

    /// <summary>
    /// Set rate I gain for an axis
    /// Parameters: ATC_RAT_RLL_I, ATC_RAT_PIT_I, ATC_RAT_YAW_I
    /// </summary>
    Task<bool> SetRateIAsync(TuningAxis axis, float value);

    /// <summary>
    /// Set rate D gain for an axis
    /// Parameters: ATC_RAT_RLL_D, ATC_RAT_PIT_D, ATC_RAT_YAW_D
    /// </summary>
    Task<bool> SetRateDAsync(TuningAxis axis, float value);

    /// <summary>
    /// Set rate feed-forward for an axis
    /// Parameters: ATC_RAT_RLL_FF, ATC_RAT_PIT_FF, ATC_RAT_YAW_FF
    /// </summary>
    Task<bool> SetRateFFAsync(TuningAxis axis, float value);

    /// <summary>
    /// Set rate filter for an axis
    /// Parameters: ATC_RAT_RLL_FLTD, ATC_RAT_PIT_FLTD, ATC_RAT_YAW_FLTD
    /// </summary>
    Task<bool> SetRateFilterAsync(TuningAxis axis, float value);

    #endregion

    #region AutoTune

    /// <summary>
    /// Get AutoTune configuration
    /// </summary>
    Task<AutoTuneSettings?> GetAutoTuneSettingsAsync();

    /// <summary>
    /// Update AutoTune configuration
    /// </summary>
    Task<bool> UpdateAutoTuneSettingsAsync(AutoTuneSettings settings);

    /// <summary>
    /// Set which axes to include in AutoTune
    /// Parameter: AUTOTUNE_AXES
    /// </summary>
    Task<bool> SetAutoTuneAxesAsync(AutoTuneAxes axes);

    /// <summary>
    /// Set AutoTune aggressiveness
    /// Parameter: AUTOTUNE_AGGR
    /// </summary>
    Task<bool> SetAutoTuneAggressivenessAsync(float value);

    /// <summary>
    /// Assign RC channel for AutoTune switch
    /// Sets RCx_OPTION = 17 for the specified channel
    /// </summary>
    Task<bool> SetAutoTuneSwitchChannelAsync(AutoTuneChannel channel);

    #endregion

    #region In-Flight Tuning

    /// <summary>
    /// Get in-flight tuning configuration
    /// </summary>
    Task<InFlightTuningSettings?> GetInFlightTuningSettingsAsync();

    /// <summary>
    /// Update in-flight tuning configuration
    /// </summary>
    Task<bool> UpdateInFlightTuningSettingsAsync(InFlightTuningSettings settings);

    /// <summary>
    /// Set the parameter to tune via RC6
    /// Parameter: TUNE
    /// </summary>
    Task<bool> SetTuneOptionAsync(InFlightTuningOption option);

    /// <summary>
    /// Set tuning range minimum
    /// Parameter: TUNE_MIN
    /// </summary>
    Task<bool> SetTuneMinAsync(float value);

    /// <summary>
    /// Set tuning range maximum
    /// Parameter: TUNE_MAX
    /// </summary>
    Task<bool> SetTuneMaxAsync(float value);

    #endregion

    #region Full Configuration

    /// <summary>
    /// Get complete PID tuning configuration from vehicle
    /// </summary>
    Task<PidTuningConfiguration?> GetFullConfigurationAsync();

    /// <summary>
    /// Apply complete PID tuning configuration to vehicle
    /// </summary>
    Task<bool> ApplyFullConfigurationAsync(PidTuningConfiguration config);

    /// <summary>
    /// Apply default/recommended PID configuration
    /// </summary>
    Task<bool> ApplyDefaultConfigurationAsync();

    /// <summary>
    /// Validate PID configuration
    /// Returns list of warnings/issues
    /// </summary>
    List<string> ValidateConfiguration(PidTuningConfiguration config);

    #endregion

    #region Parameter Info

    /// <summary>
    /// Get parameter information for a specific PID parameter
    /// </summary>
    PidParameterInfo GetParameterInfo(string parameterName);

    /// <summary>
    /// Get all available tuning parameters with their info
    /// </summary>
    IEnumerable<PidParameterInfo> GetAllParameterInfo();

    /// <summary>
    /// Get available in-flight tuning options
    /// </summary>
    IEnumerable<(InFlightTuningOption Option, string Label, string Description)> GetInFlightTuningOptions();

    #endregion
}

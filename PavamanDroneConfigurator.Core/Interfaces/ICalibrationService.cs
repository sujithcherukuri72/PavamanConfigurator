using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for drone sensor calibration.
/// Uses MAVLink MAV_CMD_PREFLIGHT_CALIBRATION (command 241) for real calibration.
/// </summary>
public interface ICalibrationService
{
    /// <summary>
    /// Start a specific calibration type.
    /// Sends MAV_CMD_PREFLIGHT_CALIBRATION with appropriate parameters.
    /// </summary>
    Task<bool> StartCalibrationAsync(CalibrationType type);

    /// <summary>
    /// Cancel the current calibration in progress.
    /// </summary>
    Task<bool> CancelCalibrationAsync();

    /// <summary>
    /// Accept/confirm the current calibration step (for multi-step calibrations).
    /// Used during accelerometer calibration to confirm each position.
    /// </summary>
    Task<bool> AcceptCalibrationStepAsync();

    /// <summary>
    /// Start accelerometer calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param5 = 1 (simple) or 4 (full 6-axis)
    /// </summary>
    Task<bool> StartAccelerometerCalibrationAsync(bool fullSixAxis = true);

    /// <summary>
    /// Start compass/magnetometer calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param2 = 1 (mag) or param2 = 76 (onboard mag cal)
    /// </summary>
    Task<bool> StartCompassCalibrationAsync(bool onboardCalibration = false);

    /// <summary>
    /// Start gyroscope calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param1 = 1
    /// </summary>
    Task<bool> StartGyroscopeCalibrationAsync();

    /// <summary>
    /// Start level horizon calibration (trims).
    /// MAV_CMD_PREFLIGHT_CALIBRATION param5 = 2
    /// </summary>
    Task<bool> StartLevelHorizonCalibrationAsync();

    /// <summary>
    /// Start barometer calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param3 = 1
    /// </summary>
    Task<bool> StartBarometerCalibrationAsync();

    /// <summary>
    /// Start airspeed sensor calibration (for planes).
    /// MAV_CMD_PREFLIGHT_CALIBRATION param4 = 1
    /// </summary>
    Task<bool> StartAirspeedCalibrationAsync();

    /// <summary>
    /// Reboot the flight controller.
    /// MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN
    /// </summary>
    Task<bool> RebootFlightControllerAsync();

    /// <summary>
    /// Get the current calibration state.
    /// </summary>
    CalibrationStateModel? CurrentState { get; }

    /// <summary>
    /// Whether a calibration is currently in progress.
    /// </summary>
    bool IsCalibrating { get; }

    /// <summary>
    /// Event raised when calibration state changes.
    /// </summary>
    event EventHandler<CalibrationStateModel>? CalibrationStateChanged;

    /// <summary>
    /// Event raised when calibration progress updates.
    /// </summary>
    event EventHandler<CalibrationProgressEventArgs>? CalibrationProgressChanged;

    /// <summary>
    /// Event raised when a calibration step requires user action.
    /// </summary>
    event EventHandler<CalibrationStepEventArgs>? CalibrationStepRequired;
}

/// <summary>
/// Event args for calibration progress updates.
/// </summary>
public class CalibrationProgressEventArgs : EventArgs
{
    public CalibrationType Type { get; set; }
    public int ProgressPercent { get; set; }
    public string? StatusText { get; set; }
    public int? CurrentStep { get; set; }
    public int? TotalSteps { get; set; }
}

/// <summary>
/// Event args for calibration step requirements.
/// </summary>
public class CalibrationStepEventArgs : EventArgs
{
    public CalibrationType Type { get; set; }
    public CalibrationStep Step { get; set; }
    public string? Instructions { get; set; }
}

/// <summary>
/// Calibration steps for accelerometer 6-axis calibration.
/// </summary>
public enum CalibrationStep
{
    /// <summary>Place vehicle level</summary>
    Level,
    /// <summary>Place vehicle on its left side</summary>
    LeftSide,
    /// <summary>Place vehicle on its right side</summary>
    RightSide,
    /// <summary>Place vehicle nose down</summary>
    NoseDown,
    /// <summary>Place vehicle nose up</summary>
    NoseUp,
    /// <summary>Place vehicle on its back (upside down)</summary>
    Back,
    /// <summary>Rotate vehicle for compass calibration</summary>
    Rotate,
    /// <summary>Keep vehicle still</summary>
    KeepStill
}

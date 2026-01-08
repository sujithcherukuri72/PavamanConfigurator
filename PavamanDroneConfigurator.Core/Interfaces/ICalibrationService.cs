using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Production-ready calibration service interface.
/// Firmware is the SINGLE SOURCE OF TRUTH - UI never decides success.
/// All state changes are driven by STATUSTEXT messages from the flight controller.
/// </summary>
public interface ICalibrationService
{
    #region Calibration Operations

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
    /// Accept/confirm the current calibration step.
    /// For accelerometer: sends MAV_CMD_ACCELCAL_VEHICLE_POS to FC.
    /// FC validates the position and decides whether to accept.
    /// </summary>
    Task<bool> AcceptCalibrationStepAsync();

    /// <summary>
    /// Start accelerometer calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param5 = 1 (simple) or 4 (full 6-axis)
    /// </summary>
    Task<bool> StartAccelerometerCalibrationAsync(bool fullSixAxis = true);

    /// <summary>
    /// Start compass/magnetometer calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param2 = 1 (mag) or 76 (onboard mag cal)
    /// </summary>
    Task<bool> StartCompassCalibrationAsync(bool onboardCalibration = false);

    /// <summary>
    /// Start gyroscope calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param1 = 1
    /// Vehicle must remain completely still.
    /// </summary>
    Task<bool> StartGyroscopeCalibrationAsync();

    /// <summary>
    /// Start level horizon calibration (trims).
    /// MAV_CMD_PREFLIGHT_CALIBRATION param5 = 2
    /// Vehicle must be perfectly level.
    /// </summary>
    Task<bool> StartLevelHorizonCalibrationAsync();

    /// <summary>
    /// Start barometer calibration.
    /// MAV_CMD_PREFLIGHT_CALIBRATION param3 = 1
    /// Vehicle must be stationary.
    /// </summary>
    Task<bool> StartBarometerCalibrationAsync();

    /// <summary>
    /// Start airspeed sensor calibration (for planes).
    /// MAV_CMD_PREFLIGHT_CALIBRATION param4 = 1
    /// </summary>
    Task<bool> StartAirspeedCalibrationAsync();

    /// <summary>
    /// Reboot the flight controller.
    /// MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN param1 = 1
    /// </summary>
    Task<bool> RebootFlightControllerAsync();

    #endregion

    #region State Properties

    /// <summary>
    /// Get the current calibration state (for UI binding).
    /// </summary>
    CalibrationStateModel? CurrentState { get; }

    /// <summary>
    /// Whether a calibration is currently in progress.
    /// </summary>
    bool IsCalibrating { get; }

    /// <summary>
    /// Current state machine state (detailed).
    /// </summary>
    CalibrationStateMachine StateMachineState { get; }

    /// <summary>
    /// Get the current calibration diagnostics.
    /// </summary>
    CalibrationDiagnostics? CurrentDiagnostics { get; }

    #endregion

    #region Events

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
    /// FC is requesting a specific vehicle position.
    /// </summary>
    event EventHandler<CalibrationStepEventArgs>? CalibrationStepRequired;

    /// <summary>
    /// Event raised when FC sends a STATUSTEXT during calibration.
    /// For diagnostic logging and advanced UI.
    /// </summary>
    event EventHandler<CalibrationStatusTextEventArgs>? StatusTextReceived;

    #endregion
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
    public CalibrationStateMachine StateMachine { get; set; }
}

/// <summary>
/// Event args for calibration step requirements.
/// Raised when FC requests a specific vehicle position.
/// </summary>
public class CalibrationStepEventArgs : EventArgs
{
    public CalibrationType Type { get; set; }
    public CalibrationStep Step { get; set; }
    public string? Instructions { get; set; }
    public bool CanConfirm { get; set; } = true;
}

/// <summary>
/// Event args for FC STATUSTEXT messages during calibration.
/// </summary>
public class CalibrationStatusTextEventArgs : EventArgs
{
    public byte Severity { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
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

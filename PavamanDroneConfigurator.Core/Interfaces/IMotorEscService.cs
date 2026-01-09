using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for motor testing and ESC configuration.
/// Uses MAVLink DO_MOTOR_TEST command for motor testing.
/// </summary>
public interface IMotorEscService
{
    /// <summary>
    /// Event raised when motor/ESC settings are updated from the vehicle
    /// </summary>
    event EventHandler<MotorEscSettings>? SettingsChanged;

    /// <summary>
    /// Event raised when motor status changes during testing
    /// </summary>
    event EventHandler<MotorStatus>? MotorStatusChanged;

    /// <summary>
    /// Event raised when motor test completes
    /// </summary>
    event EventHandler<(int MotorNumber, bool Success, string Message)>? MotorTestCompleted;

    /// <summary>
    /// Get the current motor/ESC settings from the vehicle
    /// </summary>
    Task<MotorEscSettings?> GetSettingsAsync();

    /// <summary>
    /// Update motor/ESC settings on the vehicle
    /// </summary>
    Task<bool> UpdateSettingsAsync(MotorEscSettings settings);

    /// <summary>
    /// Start motor test for a specific motor
    /// Uses MAVLink DO_MOTOR_TEST command (MAV_CMD_DO_MOTOR_TEST = 209)
    /// </summary>
    /// <param name="request">Motor test parameters</param>
    Task<bool> StartMotorTestAsync(MotorTestRequest request);

    /// <summary>
    /// Stop all motor tests immediately
    /// Sends motor test with 0 throttle to all motors
    /// </summary>
    Task<bool> StopAllMotorTestsAsync();

    /// <summary>
    /// Stop motor test for a specific motor
    /// </summary>
    Task<bool> StopMotorTestAsync(int motorNumber);

    /// <summary>
    /// Test all motors in sequence
    /// </summary>
    /// <param name="throttlePercent">Throttle percentage (0-100)</param>
    /// <param name="durationSeconds">Duration per motor in seconds</param>
    /// <param name="delayBetweenMs">Delay between motor tests in ms</param>
    Task<bool> TestAllMotorsSequentialAsync(float throttlePercent, float durationSeconds, int delayBetweenMs = 500);

    /// <summary>
    /// Start ESC calibration process
    /// Sets ESC_CALIBRATION parameter to initiate calibration on next boot
    /// </summary>
    Task<bool> StartEscCalibrationAsync();

    /// <summary>
    /// Cancel/disable ESC calibration
    /// </summary>
    Task<bool> CancelEscCalibrationAsync();

    /// <summary>
    /// Get detected motor count from vehicle
    /// </summary>
    Task<int> GetMotorCountAsync();

    /// <summary>
    /// Set motor output type (PWM, DShot, etc.)
    /// </summary>
    Task<bool> SetMotorOutputTypeAsync(MotorOutputType outputType);

    /// <summary>
    /// Set motor PWM range
    /// </summary>
    Task<bool> SetPwmRangeAsync(int minPwm, int maxPwm);

    /// <summary>
    /// Get current motor status for all motors
    /// </summary>
    Task<List<MotorStatus>> GetAllMotorStatusAsync();

    /// <summary>
    /// Check if motors are safe to test (disarmed, props safety acknowledged)
    /// </summary>
    bool IsSafeToTest { get; }

    /// <summary>
    /// Set safety acknowledgement for motor testing
    /// User must confirm propellers are removed
    /// </summary>
    void AcknowledgeSafetyWarning(bool propsRemoved);

    /// <summary>
    /// Whether safety has been acknowledged
    /// </summary>
    bool SafetyAcknowledged { get; }

    /// <summary>
    /// Get recommended motor settings based on frame type
    /// </summary>
    MotorEscSettings GetRecommendedSettings(int motorCount, MotorOutputType outputType);

    /// <summary>
    /// Validate motor/ESC settings
    /// </summary>
    List<string> ValidateSettings(MotorEscSettings settings);
}

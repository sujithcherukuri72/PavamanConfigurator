using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Infrastructure.MAVLink;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Calibration service that sends real MAVLink calibration commands.
/// Uses MAV_CMD_PREFLIGHT_CALIBRATION (command 241) for sensor calibration.
/// Monitors STATUSTEXT messages for calibration progress and instructions.
/// </summary>
public class CalibrationService : ICalibrationService
{
    private readonly ILogger<CalibrationService> _logger;
    private readonly IConnectionService _connectionService;
    
    private CalibrationStateModel _currentState = new();
    private bool _isCalibrating;
    private CalibrationType _currentCalibrationType;
    private TaskCompletionSource<bool>? _calibrationComplete;
    private int _accelCalibrationStep;

    // Calibration status text patterns (from ArduPilot)
    private static readonly string[] AccelCalibrationPositions = 
    {
        "level", "LEFT", "RIGHT", "DOWN", "UP", "BACK"
    };

    public CalibrationStateModel? CurrentState => _currentState;
    public bool IsCalibrating => _isCalibrating;

    public event EventHandler<CalibrationStateModel>? CalibrationStateChanged;
    public event EventHandler<CalibrationProgressEventArgs>? CalibrationProgressChanged;
    public event EventHandler<CalibrationStepEventArgs>? CalibrationStepRequired;

    public CalibrationService(
        ILogger<CalibrationService> logger,
        IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
    }

    public Task<bool> StartCalibrationAsync(CalibrationType type)
    {
        return type switch
        {
            CalibrationType.Accelerometer => StartAccelerometerCalibrationAsync(true),
            CalibrationType.Compass => StartCompassCalibrationAsync(false),
            CalibrationType.Gyroscope => StartGyroscopeCalibrationAsync(),
            CalibrationType.Barometer => StartBarometerCalibrationAsync(),
            CalibrationType.LevelHorizon => StartLevelHorizonCalibrationAsync(),
            _ => Task.FromResult(false)
        };
    }

    public async Task<bool> StartAccelerometerCalibrationAsync(bool fullSixAxis = true)
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start accelerometer calibration - not connected");
            return false;
        }

        if (_isCalibrating)
        {
            _logger.LogWarning("Calibration already in progress");
            return false;
        }

        try
        {
            _isCalibrating = true;
            _currentCalibrationType = CalibrationType.Accelerometer;
            _accelCalibrationStep = 0;
            _calibrationComplete = new TaskCompletionSource<bool>();

            UpdateState(CalibrationState.InProgress, 0, "Starting accelerometer calibration...");

            // Request first position
            RaiseCalibrationStepRequired(CalibrationStep.Level, "Place vehicle level and press Accept");

            // MAV_CMD_PREFLIGHT_CALIBRATION: param5 = 4 for 6-axis, 1 for simple
            _connectionService.SendPreflightCalibration(
                gyro: 0,
                mag: 0,
                groundPressure: 0,
                airspeed: 0,
                accel: fullSixAxis ? 4 : 1);

            _logger.LogInformation("Accelerometer calibration command sent (6-axis: {SixAxis})", fullSixAxis);
            
            // Wait a moment for command to be processed
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting accelerometer calibration");
            UpdateState(CalibrationState.Failed, 0, $"Failed: {ex.Message}");
            _isCalibrating = false;
            return false;
        }
    }

    public async Task<bool> StartCompassCalibrationAsync(bool onboardCalibration = false)
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start compass calibration - not connected");
            return false;
        }

        if (_isCalibrating)
        {
            _logger.LogWarning("Calibration already in progress");
            return false;
        }

        try
        {
            _isCalibrating = true;
            _currentCalibrationType = CalibrationType.Compass;
            _calibrationComplete = new TaskCompletionSource<bool>();

            UpdateState(CalibrationState.InProgress, 0, "Starting compass calibration...");
            RaiseCalibrationStepRequired(CalibrationStep.Rotate, "Rotate the vehicle in all orientations");

            // MAV_CMD_PREFLIGHT_CALIBRATION: param2 = 1 for mag, or 76 for onboard mag cal
            _connectionService.SendPreflightCalibration(
                gyro: 0,
                mag: onboardCalibration ? 76 : 1,
                groundPressure: 0,
                airspeed: 0,
                accel: 0);

            _logger.LogInformation("Compass calibration command sent (onboard: {Onboard})", onboardCalibration);
            
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting compass calibration");
            UpdateState(CalibrationState.Failed, 0, $"Failed: {ex.Message}");
            _isCalibrating = false;
            return false;
        }
    }

    public async Task<bool> StartGyroscopeCalibrationAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start gyroscope calibration - not connected");
            return false;
        }

        if (_isCalibrating)
        {
            _logger.LogWarning("Calibration already in progress");
            return false;
        }

        try
        {
            _isCalibrating = true;
            _currentCalibrationType = CalibrationType.Gyroscope;

            UpdateState(CalibrationState.InProgress, 0, "Starting gyroscope calibration...");
            RaiseCalibrationStepRequired(CalibrationStep.KeepStill, "Keep the vehicle completely still");

            // MAV_CMD_PREFLIGHT_CALIBRATION: param1 = 1 for gyro
            _connectionService.SendPreflightCalibration(
                gyro: 1,
                mag: 0,
                groundPressure: 0,
                airspeed: 0,
                accel: 0);

            _logger.LogInformation("Gyroscope calibration command sent");
            
            // Gyro cal is usually quick - wait a bit then mark complete
            await Task.Delay(3000);
            
            if (_isCalibrating && _currentCalibrationType == CalibrationType.Gyroscope)
            {
                UpdateState(CalibrationState.Completed, 100, "Gyroscope calibration complete");
                _isCalibrating = false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting gyroscope calibration");
            UpdateState(CalibrationState.Failed, 0, $"Failed: {ex.Message}");
            _isCalibrating = false;
            return false;
        }
    }

    public async Task<bool> StartLevelHorizonCalibrationAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start level horizon calibration - not connected");
            return false;
        }

        if (_isCalibrating)
        {
            _logger.LogWarning("Calibration already in progress");
            return false;
        }

        try
        {
            _isCalibrating = true;
            _currentCalibrationType = CalibrationType.LevelHorizon;

            UpdateState(CalibrationState.InProgress, 0, "Starting level horizon calibration...");
            RaiseCalibrationStepRequired(CalibrationStep.Level, "Place vehicle on a level surface");

            // MAV_CMD_PREFLIGHT_CALIBRATION: param5 = 2 for level
            _connectionService.SendPreflightCalibration(
                gyro: 0,
                mag: 0,
                groundPressure: 0,
                airspeed: 0,
                accel: 2);

            _logger.LogInformation("Level horizon calibration command sent");
            
            // Level cal is usually quick
            await Task.Delay(2000);
            
            if (_isCalibrating && _currentCalibrationType == CalibrationType.LevelHorizon)
            {
                UpdateState(CalibrationState.Completed, 100, "Level horizon calibration complete");
                _isCalibrating = false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting level horizon calibration");
            UpdateState(CalibrationState.Failed, 0, $"Failed: {ex.Message}");
            _isCalibrating = false;
            return false;
        }
    }

    public async Task<bool> StartBarometerCalibrationAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start barometer calibration - not connected");
            return false;
        }

        if (_isCalibrating)
        {
            _logger.LogWarning("Calibration already in progress");
            return false;
        }

        try
        {
            _isCalibrating = true;
            _currentCalibrationType = CalibrationType.Barometer;

            UpdateState(CalibrationState.InProgress, 0, "Starting barometer calibration...");

            // MAV_CMD_PREFLIGHT_CALIBRATION: param3 = 1 for ground pressure / baro
            _connectionService.SendPreflightCalibration(
                gyro: 0,
                mag: 0,
                groundPressure: 1,
                airspeed: 0,
                accel: 0);

            _logger.LogInformation("Barometer calibration command sent");
            
            // Baro cal is quick
            await Task.Delay(2000);
            
            if (_isCalibrating && _currentCalibrationType == CalibrationType.Barometer)
            {
                UpdateState(CalibrationState.Completed, 100, "Barometer calibration complete");
                _isCalibrating = false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting barometer calibration");
            UpdateState(CalibrationState.Failed, 0, $"Failed: {ex.Message}");
            _isCalibrating = false;
            return false;
        }
    }

    public async Task<bool> StartAirspeedCalibrationAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start airspeed calibration - not connected");
            return false;
        }

        try
        {
            _isCalibrating = true;

            UpdateState(CalibrationState.InProgress, 0, "Starting airspeed calibration...");

            // MAV_CMD_PREFLIGHT_CALIBRATION: param4 = 1 for airspeed
            _connectionService.SendPreflightCalibration(
                gyro: 0,
                mag: 0,
                groundPressure: 0,
                airspeed: 1,
                accel: 0);

            _logger.LogInformation("Airspeed calibration command sent");
            
            await Task.Delay(2000);
            
            UpdateState(CalibrationState.Completed, 100, "Airspeed calibration complete");
            _isCalibrating = false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting airspeed calibration");
            UpdateState(CalibrationState.Failed, 0, $"Failed: {ex.Message}");
            _isCalibrating = false;
            return false;
        }
    }

    public Task<bool> AcceptCalibrationStepAsync()
    {
        if (!_isCalibrating)
        {
            _logger.LogWarning("No calibration in progress");
            return Task.FromResult(false);
        }

        if (_currentCalibrationType == CalibrationType.Accelerometer)
        {
            _accelCalibrationStep++;
            int progress = (_accelCalibrationStep * 100) / 6;

            if (_accelCalibrationStep >= 6)
            {
                UpdateState(CalibrationState.Completed, 100, "Accelerometer calibration complete!");
                _isCalibrating = false;
                _calibrationComplete?.TrySetResult(true);
                return Task.FromResult(true);
            }

            // Request next position
            var step = _accelCalibrationStep switch
            {
                1 => CalibrationStep.LeftSide,
                2 => CalibrationStep.RightSide,
                3 => CalibrationStep.NoseDown,
                4 => CalibrationStep.NoseUp,
                5 => CalibrationStep.Back,
                _ => CalibrationStep.Level
            };

            var position = AccelCalibrationPositions[_accelCalibrationStep];
            UpdateState(CalibrationState.InProgress, progress, $"Position {_accelCalibrationStep + 1}/6: Place vehicle {position}");
            RaiseCalibrationStepRequired(step, $"Place vehicle {position} and press Accept");
        }

        return Task.FromResult(true);
    }

    public Task<bool> CancelCalibrationAsync()
    {
        _logger.LogInformation("Cancelling calibration");
        
        UpdateState(CalibrationState.Failed, 0, "Calibration cancelled by user");
        _isCalibrating = false;
        _calibrationComplete?.TrySetResult(false);

        return Task.FromResult(true);
    }

    public Task<bool> RebootFlightControllerAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot reboot - not connected");
            return Task.FromResult(false);
        }

        try
        {
            _logger.LogInformation("Sending reboot command");
            _connectionService.SendPreflightReboot(autopilot: 1, companion: 0);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending reboot command");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Handle STATUSTEXT message for calibration progress.
    /// Called by ConnectionService when STATUSTEXT is received.
    /// </summary>
    public void HandleStatusText(byte severity, string text)
    {
        if (!_isCalibrating)
            return;

        _logger.LogDebug("Calibration status: {Text}", text);

        // Check for completion messages
        if (text.Contains("calibration successful", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("calibration done", StringComparison.OrdinalIgnoreCase))
        {
            UpdateState(CalibrationState.Completed, 100, text);
            _isCalibrating = false;
            _calibrationComplete?.TrySetResult(true);
            return;
        }

        // Check for failure messages
        if (text.Contains("calibration failed", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("calibration cancelled", StringComparison.OrdinalIgnoreCase))
        {
            UpdateState(CalibrationState.Failed, 0, text);
            _isCalibrating = false;
            _calibrationComplete?.TrySetResult(false);
            return;
        }

        // Check for position instructions (accel cal)
        foreach (var position in AccelCalibrationPositions)
        {
            if (text.Contains(position, StringComparison.OrdinalIgnoreCase) &&
                text.Contains("place", StringComparison.OrdinalIgnoreCase))
            {
                var step = position.ToLowerInvariant() switch
                {
                    "level" => CalibrationStep.Level,
                    "left" => CalibrationStep.LeftSide,
                    "right" => CalibrationStep.RightSide,
                    "down" => CalibrationStep.NoseDown,
                    "up" => CalibrationStep.NoseUp,
                    "back" => CalibrationStep.Back,
                    _ => CalibrationStep.Level
                };
                
                RaiseCalibrationStepRequired(step, text);
                break;
            }
        }

        // Update progress message
        _currentState.Message = text;
        CalibrationStateChanged?.Invoke(this, _currentState);
    }

    private void UpdateState(CalibrationState state, int progress, string message)
    {
        _currentState = new CalibrationStateModel
        {
            Type = _currentCalibrationType,
            State = state,
            Progress = progress,
            Message = message
        };

        CalibrationStateChanged?.Invoke(this, _currentState);

        CalibrationProgressChanged?.Invoke(this, new CalibrationProgressEventArgs
        {
            Type = _currentCalibrationType,
            ProgressPercent = progress,
            StatusText = message,
            CurrentStep = _accelCalibrationStep + 1,
            TotalSteps = _currentCalibrationType == CalibrationType.Accelerometer ? 6 : 1
        });
    }

    private void RaiseCalibrationStepRequired(CalibrationStep step, string instructions)
    {
        CalibrationStepRequired?.Invoke(this, new CalibrationStepEventArgs
        {
            Type = _currentCalibrationType,
            Step = step,
            Instructions = instructions
        });
    }
}

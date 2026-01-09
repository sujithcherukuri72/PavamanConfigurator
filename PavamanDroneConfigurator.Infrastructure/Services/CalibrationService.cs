using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using System.Text.RegularExpressions;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Production-ready Calibration Service implementing Mission Planner behavior.
/// 
/// CRITICAL PRINCIPLES:
/// 1. Firmware is the SINGLE SOURCE OF TRUTH
/// 2. UI NEVER decides calibration success
/// 3. Steps advance ONLY via STATUSTEXT messages from FC
/// 4. All state transitions are driven by FC responses
/// 
/// Accelerometer 6-axis calibration flow:
/// 1. Send MAV_CMD_PREFLIGHT_CALIBRATION with param5=4
/// 2. Wait for COMMAND_ACK (ACCEPTED or IN_PROGRESS)
/// 3. FC sends STATUSTEXT "Place vehicle level..."
/// 4. User positions vehicle, clicks confirm
/// 5. App sends MAV_CMD_ACCELCAL_VEHICLE_POS(1)
/// 6. FC samples, validates orientation
/// 7. FC sends STATUSTEXT with result (next position or error)
/// 8. Repeat for positions 2-6
/// 9. FC sends "calibration successful" STATUSTEXT
/// 
/// Position mapping (ArduPilot):
/// 1=Level, 2=Left, 3=Right, 4=NoseDown, 5=NoseUp, 6=Back
/// </summary>
public class CalibrationService : ICalibrationService
{
    private readonly ILogger<CalibrationService> _logger;
    private readonly IConnectionService _connectionService;
    
    // State
    private CalibrationStateModel _currentState = new();
    private CalibrationDiagnostics? _currentDiagnostics;
    private CalibrationStateMachine _stateMachine = CalibrationStateMachine.Idle;
    private CalibrationType _currentCalibrationType;
    private int _currentPositionNumber;
    private bool _isCalibrating;
    private readonly object _lock = new();
    
    // Timeouts
    private const int COMMAND_ACK_TIMEOUT_MS = 5000;
    private const int POSITION_SAMPLE_TIMEOUT_MS = 30000;
    private const int CALIBRATION_TIMEOUT_MS = 300000; // 5 minutes max
    
    // Position constants
    private static readonly string[] AccelPositionNames = 
        { "LEVEL", "LEFT", "RIGHT", "NOSE DOWN", "NOSE UP", "BACK" };
    
    private static readonly string[] AccelPositionInstructions =
    {
        "Place vehicle LEVEL on a flat surface, then click 'Click When In Position'",
        "Place vehicle on its LEFT side, then click 'Click When In Position'",
        "Place vehicle on its RIGHT side, then click 'Click When In Position'",
        "Place vehicle with NOSE DOWN, then click 'Click When In Position'",
        "Place vehicle with NOSE UP, then click 'Click When In Position'",
        "Place vehicle on its BACK (upside down), then click 'Click When In Position'"
    };

    // ArduPilot STATUSTEXT keywords for pattern matching
    private static class StatusKeywords
    {
        // Completion
        public static readonly string[] Complete = { "calibration successful", "calibration complete", 
            "calibration done", "cal complete", "cal done" };
        
        // Failure
        public static readonly string[] Failed = { "calibration failed", "calibration cancelled", 
            "cal failed", "failed" };
        
        // Position requests
        public const string Place = "place";
        public const string Level = "level";
        public const string Left = "left";
        public const string Right = "right";
        public const string NoseDown = "nose down";
        public const string NoseUp = "nose up";
        public const string Back = "back";
        public const string Upside = "upside";
        
        // Sampling
        public static readonly string[] Sampling = { "sampling", "reading", "detected", "hold still" };
        
        // Progress
        public const string Progress = "progress";
        public const string Percent = "%";
    }

    public CalibrationStateModel? CurrentState => _currentState;
    public bool IsCalibrating => _isCalibrating;
    public CalibrationStateMachine StateMachineState => _stateMachine;
    public CalibrationDiagnostics? CurrentDiagnostics => _currentDiagnostics;

    public event EventHandler<CalibrationStateModel>? CalibrationStateChanged;
    public event EventHandler<CalibrationProgressEventArgs>? CalibrationProgressChanged;
    public event EventHandler<CalibrationStepEventArgs>? CalibrationStepRequired;
    public event EventHandler<CalibrationStatusTextEventArgs>? StatusTextReceived;

    public CalibrationService(
        ILogger<CalibrationService> logger,
        IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
        
        _connectionService.StatusTextReceived += OnStatusTextReceived;
        _connectionService.CommandAckReceived += OnCommandAckReceived;
    }

    #region Event Handlers

    private void OnStatusTextReceived(object? sender, StatusTextEventArgs e)
    {
        // Always log STATUSTEXT during calibration
        if (_isCalibrating)
        {
            _currentDiagnostics?.AddStatusText(e.Severity, e.Text);
            
            StatusTextReceived?.Invoke(this, new CalibrationStatusTextEventArgs
            {
                Severity = e.Severity,
                Text = e.Text
            });
        }
        
        HandleStatusText(e.Severity, e.Text);
    }

    private void OnCommandAckReceived(object? sender, CommandAckEventArgs e)
    {
        if (!_isCalibrating)
            return;
        
        _currentDiagnostics?.AddCommandAck(e.Command, e.Result);
        _logger.LogInformation("COMMAND_ACK: cmd={Command} result={Result}", e.Command, e.Result);

        // MAV_CMD_PREFLIGHT_CALIBRATION = 241
        if (e.Command == 241)
        {
            HandleCalibrationCommandAck(e.Result);
        }
        // MAV_CMD_ACCELCAL_VEHICLE_POS = 42429
        else if (e.Command == 42429)
        {
            HandleAccelCalVehiclePosAck(e.Result);
        }
    }

    private void HandleCalibrationCommandAck(byte result)
    {
        var mavResult = (MavResult)result;
        
        if (mavResult == MavResult.Accepted || mavResult == MavResult.InProgress)
        {
            _logger.LogInformation("Calibration command accepted by FC");
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info, 
                $"FC accepted calibration command (result={mavResult})");
            
            TransitionState(CalibrationStateMachine.WaitingForInstruction);
        }
        else
        {
            _logger.LogWarning("Calibration command rejected: {Result}", mavResult);
            
            string errorMessage = mavResult switch
            {
                MavResult.TemporarilyRejected => "Calibration temporarily denied. Vehicle may be armed or busy.",
                MavResult.Denied => "Calibration denied. Check vehicle state.",
                MavResult.Unsupported => "Calibration not supported by this firmware.",
                MavResult.Failed => "Calibration failed. Check vehicle position and sensor hardware.",
                _ => $"Calibration rejected by flight controller (code: {result})"
            };
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Error, errorMessage);
            _currentDiagnostics!.LastError = errorMessage;
            
            FinishCalibration(CalibrationResult.Rejected, errorMessage);
        }
    }

    private void HandleAccelCalVehiclePosAck(byte result)
    {
        var mavResult = (MavResult)result;
        
        if (mavResult == MavResult.Accepted || mavResult == MavResult.InProgress)
        {
            _logger.LogInformation("Position {Position} acknowledged by FC, waiting for sampling", _currentPositionNumber);
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                $"FC acknowledged position {_currentPositionNumber}");
            
            TransitionState(CalibrationStateMachine.Sampling);
            
            // Record position confirmation
            var posResult = _currentDiagnostics?.AccelPositionResults
                .FirstOrDefault(p => p.Position == _currentPositionNumber);
            if (posResult != null)
            {
                posResult.FcAcceptedTime = DateTime.UtcNow;
            }
        }
        else
        {
            _logger.LogWarning("Position {Position} rejected by FC: {Result}", _currentPositionNumber, mavResult);
            
            var posName = GetPositionName(_currentPositionNumber);
            var message = $"Position {_currentPositionNumber}/6: {posName} - FC rejected. Adjust position and try again.";
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Warning, message);
            
            // Record rejection
            var posResult = _currentDiagnostics?.AccelPositionResults
                .FirstOrDefault(p => p.Position == _currentPositionNumber);
            if (posResult != null)
            {
                posResult.Accepted = false;
                posResult.FcMessage = $"Rejected: {mavResult}";
            }
            
            TransitionState(CalibrationStateMachine.PositionRejected);
            UpdateState(CalibrationState.InProgress, _currentState.Progress, message, canConfirm: true);
        }
    }

    #endregion

    #region STATUSTEXT Handling

    private void HandleStatusText(byte severity, string text)
    {
        if (!_isCalibrating)
            return;

        _logger.LogDebug("Calibration STATUSTEXT [{Severity}]: {Text}", severity, text);
        var lowerText = text.ToLowerInvariant();

        // Check for completion - FIRMWARE IS THE SOURCE OF TRUTH
        if (IsCompletionMessage(lowerText))
        {
            HandleCalibrationComplete(text);
            return;
        }

        // Check for failure
        if (IsFailureMessage(lowerText))
        {
            HandleCalibrationFailed(text);
            return;
        }

        // Handle based on calibration type
        switch (_currentCalibrationType)
        {
            case CalibrationType.Accelerometer:
                HandleAccelStatusText(lowerText, text);
                break;
            case CalibrationType.Compass:
                HandleCompassStatusText(lowerText, text);
                break;
            case CalibrationType.Gyroscope:
            case CalibrationType.Barometer:
            case CalibrationType.LevelHorizon:
                HandleSimpleCalStatusText(lowerText, text);
                break;
        }
    }

    private bool IsCompletionMessage(string lowerText)
    {
        return StatusKeywords.Complete.Any(kw => lowerText.Contains(kw));
    }

    private bool IsFailureMessage(string lowerText)
    {
        // "failed" but not "not failed" 
        if (StatusKeywords.Failed.Any(kw => lowerText.Contains(kw)))
        {
            return !lowerText.Contains("not failed") && !lowerText.Contains("didn't fail");
        }
        return false;
    }

    private void HandleAccelStatusText(string lowerText, string originalText)
    {
        // Check for position request from FC
        var requestedPosition = DetectRequestedPosition(lowerText);
        if (requestedPosition.HasValue)
        {
            lock (_lock)
            {
                _currentPositionNumber = requestedPosition.Value;
            }

            _logger.LogInformation("FC requesting position {Position}: {Name}", 
                _currentPositionNumber, GetPositionName(_currentPositionNumber));
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                $"FC requested position {_currentPositionNumber}: {GetPositionName(_currentPositionNumber)}");
            
            // Add position result entry
            _currentDiagnostics?.AccelPositionResults.Add(new AccelPositionResult
            {
                Position = _currentPositionNumber,
                PositionName = GetPositionName(_currentPositionNumber),
                Attempts = 1
            });

            TransitionState(CalibrationStateMachine.WaitingForUserPosition);
            
            int progress = ((_currentPositionNumber - 1) * 100) / 6;
            var instruction = GetPositionInstruction(_currentPositionNumber);
            var step = GetCalibrationStep(_currentPositionNumber);
            
            UpdateState(CalibrationState.InProgress, progress, 
                $"Position {_currentPositionNumber}/6: {GetPositionName(_currentPositionNumber)}", 
                canConfirm: true);
            
            RaiseCalibrationStepRequired(step, instruction);
            return;
        }

        // Check for sampling message (FC accepted and is reading)
        if (StatusKeywords.Sampling.Any(kw => lowerText.Contains(kw)))
        {
            _logger.LogInformation("FC is sampling position {Position}", _currentPositionNumber);
            
            TransitionState(CalibrationStateMachine.Sampling);
            
            int progress = (_currentPositionNumber * 100) / 6;
            UpdateState(CalibrationState.InProgress, progress,
                $"Position {_currentPositionNumber}/6: {GetPositionName(_currentPositionNumber)} - Sampling... Hold still!",
                canConfirm: false);
            
            // Update position result
            var posResult = _currentDiagnostics?.AccelPositionResults
                .FirstOrDefault(p => p.Position == _currentPositionNumber);
            if (posResult != null)
            {
                posResult.Accepted = true;
                posResult.FcMessage = originalText;
            }
            return;
        }

        // Update message but don't change state
        _currentState.Message = originalText;
        CalibrationStateChanged?.Invoke(this, _currentState);
    }

    private void HandleCompassStatusText(string lowerText, string originalText)
    {
        // Extract progress percentage
        if (lowerText.Contains(StatusKeywords.Progress) || lowerText.Contains(StatusKeywords.Percent))
        {
            var match = Regex.Match(originalText, @"(\d+)%");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
            {
                UpdateState(CalibrationState.InProgress, percent, originalText, canConfirm: false);
                
                // Update compass coverage
                if (_currentDiagnostics?.CompassCoverage != null)
                {
                    _currentDiagnostics.CompassCoverage.CompletionPercent = percent;
                }
                return;
            }
        }

        // Extract compass fitness if present
        var fitnessMatch = Regex.Match(originalText, @"fitness[:\s]+(\d+\.?\d*)", RegexOptions.IgnoreCase);
        if (fitnessMatch.Success && float.TryParse(fitnessMatch.Groups[1].Value, out var fitness))
        {
            if (_currentDiagnostics?.CompassCoverage != null)
            {
                _currentDiagnostics.CompassCoverage.Fitness = fitness;
            }
        }

        _currentState.Message = originalText;
        CalibrationStateChanged?.Invoke(this, _currentState);
    }

    private void HandleSimpleCalStatusText(string lowerText, string originalText)
    {
        // For gyro, baro, level - just update message
        _currentState.Message = originalText;
        CalibrationStateChanged?.Invoke(this, _currentState);
    }

    private int? DetectRequestedPosition(string lowerText)
    {
        // Must contain "place" to be a position request
        if (!lowerText.Contains(StatusKeywords.Place))
            return null;

        // Check positions in order of specificity
        if (lowerText.Contains(StatusKeywords.Left) && !lowerText.Contains(StatusKeywords.Right))
            return 2;
        
        if (lowerText.Contains(StatusKeywords.Right) && !lowerText.Contains(StatusKeywords.Left))
            return 3;
        
        if (lowerText.Contains(StatusKeywords.NoseDown) || 
            (lowerText.Contains("nose") && lowerText.Contains("down")))
            return 4;
        
        if (lowerText.Contains(StatusKeywords.NoseUp) || 
            (lowerText.Contains("nose") && lowerText.Contains("up")))
            return 5;
        
        if (lowerText.Contains(StatusKeywords.Back) || lowerText.Contains(StatusKeywords.Upside))
            return 6;
        
        if (lowerText.Contains(StatusKeywords.Level))
            return 1;

        return null;
    }

    private void HandleCalibrationComplete(string text)
    {
        _logger.LogInformation("Calibration completed via FC STATUSTEXT: {Text}", text);
        
        _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
            $"FC reported calibration complete: {text}");
        
        if (_currentCalibrationType == CalibrationType.Accelerometer)
        {
            lock (_lock) { _currentPositionNumber = 7; }
        }
        
        FinishCalibration(CalibrationResult.Success, 
            _currentCalibrationType == CalibrationType.Accelerometer 
                ? "Accelerometer calibration complete! Reboot recommended." 
                : text);
    }

    private void HandleCalibrationFailed(string text)
    {
        _logger.LogWarning("Calibration failed via FC STATUSTEXT: {Text}", text);
        
        _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Error,
            $"FC reported calibration failed: {text}");
        _currentDiagnostics!.LastError = text;
        
        FinishCalibration(CalibrationResult.Failed, text);
    }

    #endregion

    #region Calibration Operations

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
        if (!CanStartCalibration())
            return false;

        try
        {
            InitializeCalibration(CalibrationType.Accelerometer);
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                $"Starting accelerometer calibration (6-axis: {fullSixAxis})");

            UpdateState(CalibrationState.InProgress, 0, "Starting accelerometer calibration...", canConfirm: false);
            RaiseCalibrationStepRequired(CalibrationStep.Level, AccelPositionInstructions[0]);

            TransitionState(CalibrationStateMachine.WaitingForAck);
            
            // Send MAV_CMD_PREFLIGHT_CALIBRATION: param5 = 4 for 6-axis
            _connectionService.SendPreflightCalibration(
                gyro: 0, mag: 0, groundPressure: 0, airspeed: 0,
                accel: fullSixAxis ? 4 : 1);

            _logger.LogInformation("Sent MAV_CMD_PREFLIGHT_CALIBRATION (accel={Accel})", fullSixAxis ? 4 : 1);
            
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting accelerometer calibration");
            FinishCalibration(CalibrationResult.Failed, $"Failed to start: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StartCompassCalibrationAsync(bool onboardCalibration = false)
    {
        if (!CanStartCalibration())
            return false;

        try
        {
            InitializeCalibration(CalibrationType.Compass);
            
            _currentDiagnostics!.CompassCoverage = new CompassCoverageInfo { CompassIndex = 1 };
            _currentDiagnostics.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                $"Starting compass calibration (onboard: {onboardCalibration})");

            UpdateState(CalibrationState.InProgress, 0, "Starting compass calibration...", canConfirm: false);
            RaiseCalibrationStepRequired(CalibrationStep.Rotate,
                "Rotate the vehicle slowly in all directions. Cover all orientations until calibration completes.");

            TransitionState(CalibrationStateMachine.WaitingForAck);
            
            // MAV_CMD_PREFLIGHT_CALIBRATION: param2 = 1 (mag) or 76 (onboard)
            _connectionService.SendPreflightCalibration(
                gyro: 0, mag: onboardCalibration ? 76 : 1, groundPressure: 0, airspeed: 0, accel: 0);

            _logger.LogInformation("Sent MAV_CMD_PREFLIGHT_CALIBRATION (mag={Mag})", onboardCalibration ? 76 : 1);
            
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting compass calibration");
            FinishCalibration(CalibrationResult.Failed, $"Failed to start: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StartGyroscopeCalibrationAsync()
    {
        if (!CanStartCalibration())
            return false;

        try
        {
            InitializeCalibration(CalibrationType.Gyroscope);
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                "Starting gyroscope calibration");

            UpdateState(CalibrationState.InProgress, 0, "Starting gyroscope calibration...", canConfirm: false);
            RaiseCalibrationStepRequired(CalibrationStep.KeepStill,
                "Keep the vehicle completely still. Do not move or touch it.");

            TransitionState(CalibrationStateMachine.WaitingForAck);
            
            _connectionService.SendPreflightCalibration(
                gyro: 1, mag: 0, groundPressure: 0, airspeed: 0, accel: 0);

            _logger.LogInformation("Sent MAV_CMD_PREFLIGHT_CALIBRATION (gyro=1)");
            
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting gyroscope calibration");
            FinishCalibration(CalibrationResult.Failed, $"Failed to start: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StartLevelHorizonCalibrationAsync()
    {
        if (!CanStartCalibration())
            return false;

        try
        {
            InitializeCalibration(CalibrationType.LevelHorizon);
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                "Starting level horizon calibration");

            UpdateState(CalibrationState.InProgress, 0, "Starting level horizon calibration...", canConfirm: false);
            RaiseCalibrationStepRequired(CalibrationStep.Level,
                "Place vehicle on a perfectly level surface. Keep it completely still.");

            TransitionState(CalibrationStateMachine.WaitingForAck);
            
            // param5 = 2 for level/trim calibration
            _connectionService.SendPreflightCalibration(
                gyro: 0, mag: 0, groundPressure: 0, airspeed: 0, accel: 2);

            _logger.LogInformation("Sent MAV_CMD_PREFLIGHT_CALIBRATION (accel=2 for level)");
            
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting level horizon calibration");
            FinishCalibration(CalibrationResult.Failed, $"Failed to start: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StartBarometerCalibrationAsync()
    {
        if (!CanStartCalibration())
            return false;

        try
        {
            InitializeCalibration(CalibrationType.Barometer);
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                "Starting barometer calibration");

            UpdateState(CalibrationState.InProgress, 0, "Starting barometer calibration...", canConfirm: false);

            TransitionState(CalibrationStateMachine.WaitingForAck);
            
            _connectionService.SendPreflightCalibration(
                gyro: 0, mag: 0, groundPressure: 1, airspeed: 0, accel: 0);

            _logger.LogInformation("Sent MAV_CMD_PREFLIGHT_CALIBRATION (groundPressure=1)");
            
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting barometer calibration");
            FinishCalibration(CalibrationResult.Failed, $"Failed to start: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StartAirspeedCalibrationAsync()
    {
        if (!CanStartCalibration())
            return false;

        try
        {
            InitializeCalibration(CalibrationType.Airspeed);
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                "Starting airspeed calibration");

            UpdateState(CalibrationState.InProgress, 0, "Starting airspeed calibration...", canConfirm: false);

            TransitionState(CalibrationStateMachine.WaitingForAck);
            
            _connectionService.SendPreflightCalibration(
                gyro: 0, mag: 0, groundPressure: 0, airspeed: 1, accel: 0);

            _logger.LogInformation("Sent MAV_CMD_PREFLIGHT_CALIBRATION (airspeed=1)");
            
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting airspeed calibration");
            FinishCalibration(CalibrationResult.Failed, $"Failed to start: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// User confirms vehicle is in position.
    /// Sends MAV_CMD_ACCELCAL_VEHICLE_POS to FC for validation.
    /// FC decides whether position is correct - not the UI.
    /// </summary>
    public Task<bool> AcceptCalibrationStepAsync()
    {
        if (!_isCalibrating)
        {
            _logger.LogWarning("AcceptCalibrationStepAsync called but no calibration in progress");
            return Task.FromResult(false);
        }

        if (_stateMachine != CalibrationStateMachine.WaitingForUserPosition &&
            _stateMachine != CalibrationStateMachine.PositionRejected)
        {
            _logger.LogWarning("AcceptCalibrationStepAsync called in invalid state: {State}", _stateMachine);
            return Task.FromResult(false);
        }

        if (_currentCalibrationType == CalibrationType.Accelerometer)
        {
            _logger.LogInformation("User confirmed position {Position}, sending to FC for validation", 
                _currentPositionNumber);
            
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                $"User confirmed position {_currentPositionNumber}: {GetPositionName(_currentPositionNumber)}");
            
            // Record user confirmation time
            var posResult = _currentDiagnostics?.AccelPositionResults
                .FirstOrDefault(p => p.Position == _currentPositionNumber);
            if (posResult != null)
            {
                posResult.UserConfirmedTime = DateTime.UtcNow;
                posResult.Attempts++;
            }

            TransitionState(CalibrationStateMachine.WaitingForSampling);
            
            var posName = GetPositionName(_currentPositionNumber);
            UpdateState(CalibrationState.InProgress, _currentState.Progress,
                $"Position {_currentPositionNumber}/6: {posName} - Validating... Hold still!",
                canConfirm: false);
            
            // Send MAV_CMD_ACCELCAL_VEHICLE_POS - FC will validate
            _connectionService.SendAccelCalVehiclePos(_currentPositionNumber);
            
            _logger.LogInformation("Sent MAV_CMD_ACCELCAL_VEHICLE_POS({Position})", _currentPositionNumber);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CancelCalibrationAsync()
    {
        if (!_isCalibrating)
            return Task.FromResult(true);
        
        _logger.LogInformation("Calibration cancelled by user");
        _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info, "Calibration cancelled by user");
        
        FinishCalibration(CalibrationResult.Cancelled, "Calibration cancelled by user");
        
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

    #endregion

    #region State Management

    private bool CanStartCalibration()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start calibration - not connected");
            return false;
        }

        if (_isCalibrating)
        {
            _logger.LogWarning("Calibration already in progress");
            return false;
        }

        return true;
    }

    private void InitializeCalibration(CalibrationType type)
    {
        lock (_lock)
        {
            _isCalibrating = true;
            _currentCalibrationType = type;
            _currentPositionNumber = 1;
            _stateMachine = CalibrationStateMachine.Idle;
        }

        _currentDiagnostics = new CalibrationDiagnostics
        {
            CalibrationType = type,
            StartTime = DateTime.UtcNow
        };

        _currentState = new CalibrationStateModel
        {
            Type = type,
            State = CalibrationState.InProgress,
            StateMachine = _stateMachine,
            Diagnostics = _currentDiagnostics
        };

        _logger.LogInformation("Initialized {Type} calibration session {SessionId}", 
            type, _currentDiagnostics.SessionId);
    }

    private void TransitionState(CalibrationStateMachine newState)
    {
        var oldState = _stateMachine;
        _stateMachine = newState;
        _currentState.StateMachine = newState;
        _currentDiagnostics!.CurrentState = newState;
        
        _logger.LogDebug("State transition: {Old} -> {New}", oldState, newState);
    }

    private void UpdateState(CalibrationState state, int progress, string message, bool canConfirm)
    {
        _currentState.State = state;
        _currentState.Progress = progress;
        _currentState.Message = message;
        _currentState.CurrentPosition = _currentPositionNumber;
        _currentState.CanConfirmPosition = canConfirm;

        CalibrationStateChanged?.Invoke(this, _currentState);

        CalibrationProgressChanged?.Invoke(this, new CalibrationProgressEventArgs
        {
            Type = _currentCalibrationType,
            ProgressPercent = progress,
            StatusText = message,
            CurrentStep = _currentPositionNumber,
            TotalSteps = _currentCalibrationType == CalibrationType.Accelerometer ? 6 : 1,
            StateMachine = _stateMachine
        });
    }

    private void FinishCalibration(CalibrationResult result, string message)
    {
        var finalState = result switch
        {
            CalibrationResult.Success => CalibrationStateMachine.Completed,
            CalibrationResult.Failed => CalibrationStateMachine.Failed,
            CalibrationResult.Cancelled => CalibrationStateMachine.Cancelled,
            CalibrationResult.TimedOut => CalibrationStateMachine.TimedOut,
            CalibrationResult.Rejected => CalibrationStateMachine.Rejected,
            _ => CalibrationStateMachine.Failed
        };

        TransitionState(finalState);

        _currentDiagnostics!.EndTime = DateTime.UtcNow;
        _currentDiagnostics.Result = result;
        
        var calState = result == CalibrationResult.Success 
            ? CalibrationState.Completed 
            : CalibrationState.Failed;
        
        var progress = result == CalibrationResult.Success ? 100 : 0;
        
        UpdateState(calState, progress, message, canConfirm: false);
        
        _isCalibrating = false;

        _logger.LogInformation("Calibration finished: {Result} - {Message} (Duration: {Duration})", 
            result, message, _currentDiagnostics.Duration);
    }

    private void RaiseCalibrationStepRequired(CalibrationStep step, string instructions)
    {
        CalibrationStepRequired?.Invoke(this, new CalibrationStepEventArgs
        {
            Type = _currentCalibrationType,
            Step = step,
            Instructions = instructions,
            CanConfirm = _stateMachine == CalibrationStateMachine.WaitingForUserPosition ||
                         _stateMachine == CalibrationStateMachine.PositionRejected
        });
    }

    #endregion

    #region Helpers

    private static string GetPositionName(int position)
    {
        return position >= 1 && position <= 6 
            ? AccelPositionNames[position - 1] 
            : "UNKNOWN";
    }

    private static string GetPositionInstruction(int position)
    {
        return position >= 1 && position <= 6 
            ? AccelPositionInstructions[position - 1] 
            : "Follow FC instructions";
    }

    private static CalibrationStep GetCalibrationStep(int position)
    {
        return position switch
        {
            1 => CalibrationStep.Level,
            2 => CalibrationStep.LeftSide,
            3 => CalibrationStep.RightSide,
            4 => CalibrationStep.NoseDown,
            5 => CalibrationStep.NoseUp,
            6 => CalibrationStep.Back,
            _ => CalibrationStep.Level
        };
    }

    #endregion
}

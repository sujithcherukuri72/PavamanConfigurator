using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Mission Planner-equivalent pre-condition checker for calibration operations.
/// 
/// RELAXED INITIALIZATION GATES (Mission Planner behavior):
/// Calibration can start when:
/// - At least one valid HEARTBEAT received
/// - HEARTBEAT stable for N seconds
/// - Autopilot type is known
/// - Vehicle DISARMED
/// 
/// DO NOT REQUIRE:
/// - EKF ready/healthy
/// - GPS lock
/// - Full parameter download
/// - All SYS_STATUS health bits set
/// - STATUSTEXT "ArduPilot Ready"
/// </summary>
public class CalibrationPreConditionChecker
{
    private readonly ILogger<CalibrationPreConditionChecker> _logger;
    private readonly IConnectionService _connectionService;

    // Pre-condition state
    private bool _isArmed;
    private bool _heartbeatReceived;
    private DateTime _firstHeartbeatTime;
    private DateTime _lastHeartbeatTime;
    private byte _vehicleType;
    private byte _autopilot;
    
    // Relaxed thresholds - Mission Planner behavior
    private const int HEARTBEAT_TIMEOUT_MS = 5000;
    private const int HEARTBEAT_STABLE_SECONDS = 2; // Only 2 seconds stability required
    private const int MIN_HEARTBEATS_REQUIRED = 1; // Just 1 heartbeat is enough
    private int _heartbeatCount;

    public CalibrationPreConditionChecker(
        ILogger<CalibrationPreConditionChecker> logger,
        IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
        
        _connectionService.HeartbeatDataReceived += OnHeartbeatDataReceived;
        _connectionService.HeartbeatReceived += OnHeartbeatReceived;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    private void OnHeartbeatReceived(object? sender, EventArgs e)
    {
        _lastHeartbeatTime = DateTime.UtcNow;
        _heartbeatCount++;
        
        if (!_heartbeatReceived)
        {
            _heartbeatReceived = true;
            _firstHeartbeatTime = DateTime.UtcNow;
            _logger.LogInformation("First heartbeat received - calibration can proceed after stability check");
        }
    }

    private void OnHeartbeatDataReceived(object? sender, HeartbeatDataEventArgs e)
    {
        _lastHeartbeatTime = DateTime.UtcNow;
        _isArmed = e.IsArmed;
        _vehicleType = e.VehicleType;
        _autopilot = e.Autopilot;
        _heartbeatCount++;
        
        if (!_heartbeatReceived)
        {
            _heartbeatReceived = true;
            _firstHeartbeatTime = DateTime.UtcNow;
            _logger.LogInformation("First heartbeat received - vehicle type: {VehicleType}, autopilot: {Autopilot}", 
                e.VehicleType, e.Autopilot);
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (!connected)
        {
            Reset();
        }
    }

    /// <summary>
    /// Validates pre-conditions for calibration.
    /// Uses RELAXED gates - only checks essential conditions.
    /// </summary>
    public CalibrationPreConditionResult ValidatePreConditions(CalibrationType calibrationType)
    {
        var result = new CalibrationPreConditionResult
        {
            CalibrationType = calibrationType,
            CheckTime = DateTime.UtcNow
        };

        // Check 1: Connection state
        if (!_connectionService.IsConnected)
        {
            result.AddFailure(PreConditionFailure.NotConnected, 
                "Vehicle not connected. Establish MAVLink connection first.");
            return result;
        }

        // Check 2: At least one heartbeat received (RELAXED - just 1 needed)
        if (!_heartbeatReceived || _heartbeatCount < MIN_HEARTBEATS_REQUIRED)
        {
            result.AddFailure(PreConditionFailure.HeartbeatUnstable,
                $"Waiting for heartbeat. Received {_heartbeatCount}/{MIN_HEARTBEATS_REQUIRED}.");
            return result;
        }

        // Check 3: Heartbeat not timed out
        var timeSinceHeartbeat = DateTime.UtcNow - _lastHeartbeatTime;
        if (timeSinceHeartbeat.TotalMilliseconds > HEARTBEAT_TIMEOUT_MS)
        {
            result.AddFailure(PreConditionFailure.HeartbeatTimeout,
                $"MAVLink heartbeat timeout. No heartbeat for {timeSinceHeartbeat.TotalSeconds:F1}s.");
            return result;
        }

        // Check 4: Heartbeat stable for N seconds (RELAXED - only 2 seconds)
        var timeSinceFirstHeartbeat = DateTime.UtcNow - _firstHeartbeatTime;
        if (timeSinceFirstHeartbeat.TotalSeconds < HEARTBEAT_STABLE_SECONDS)
        {
            result.AddFailure(PreConditionFailure.HeartbeatUnstable,
                $"Waiting for stable heartbeat. {timeSinceFirstHeartbeat.TotalSeconds:F1}s of {HEARTBEAT_STABLE_SECONDS}s required.");
            return result;
        }

        // Check 5: Vehicle MUST be DISARMED (SAFETY - always required)
        if (_isArmed)
        {
            result.AddFailure(PreConditionFailure.VehicleArmed,
                "SAFETY VIOLATION: Vehicle is ARMED. Disarm the vehicle before calibration.");
            return result;
        }

        // NOTE: The following are NOT required (Mission Planner behavior):
        // - EKF ready/healthy (bypassed during calibration)
        // - GPS lock (not needed for sensor calibration)
        // - Full parameter download (can calibrate before params loaded)
        // - SYS_STATUS health bits (informational only)
        // - Specific STATUSTEXT messages (informational only)

        // Warnings only (do not block calibration)
        if (_autopilot != 3) // MAV_AUTOPILOT_ARDUPILOTMEGA = 3
        {
            result.AddWarning(PreConditionWarning.NonArduPilotAutopilot,
                $"Non-ArduPilot autopilot detected (type={_autopilot}). Calibration may behave differently.");
        }

        // Add type-specific instructions
        AddCalibrationInstructions(result, calibrationType);

        result.IsValid = result.Failures.Count == 0;
        
        if (result.IsValid)
        {
            _logger.LogInformation("Pre-conditions PASSED for {Type} calibration", calibrationType);
        }
        else
        {
            _logger.LogWarning("Pre-conditions FAILED for {Type}: {Summary}", 
                calibrationType, result.GetFailuresSummary());
        }
        
        return result;
    }

    /// <summary>
    /// Quick check if calibration can start (for UI enabling/disabling).
    /// </summary>
    public bool CanStartCalibration()
    {
        return _connectionService.IsConnected &&
               _heartbeatReceived &&
               _heartbeatCount >= MIN_HEARTBEATS_REQUIRED &&
               !_isArmed &&
               (DateTime.UtcNow - _lastHeartbeatTime).TotalMilliseconds < HEARTBEAT_TIMEOUT_MS;
    }

    /// <summary>
    /// Check if MAVLink link is stable enough for calibration.
    /// </summary>
    public bool IsLinkStable()
    {
        if (!_heartbeatReceived) return false;
        
        var timeSinceFirstHeartbeat = DateTime.UtcNow - _firstHeartbeatTime;
        var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeatTime;
        
        return timeSinceFirstHeartbeat.TotalSeconds >= HEARTBEAT_STABLE_SECONDS &&
               timeSinceLastHeartbeat.TotalMilliseconds < HEARTBEAT_TIMEOUT_MS;
    }

    private void AddCalibrationInstructions(CalibrationPreConditionResult result, CalibrationType calibrationType)
    {
        switch (calibrationType)
        {
            case CalibrationType.Accelerometer:
                result.AddInstruction("Place vehicle on a stable, level surface.");
                result.AddInstruction("You will need to position the vehicle in 6 different orientations.");
                result.AddInstruction("Ensure propellers are REMOVED or motor outputs are DISABLED.");
                break;
            case CalibrationType.Compass:
                result.AddInstruction("Move vehicle away from metal objects and electronics.");
                result.AddInstruction("Ensure adequate space to rotate vehicle in all directions.");
                result.AddInstruction("Ensure propellers are REMOVED for safety during rotation.");
                break;
            case CalibrationType.Gyroscope:
                result.AddInstruction("Place vehicle on stable surface.");
                result.AddInstruction("Do NOT touch or move the vehicle during calibration.");
                break;
            case CalibrationType.LevelHorizon:
                result.AddInstruction("Place vehicle on a PERFECTLY LEVEL surface.");
                result.AddInstruction("Use a bubble level if available.");
                break;
            case CalibrationType.Barometer:
                result.AddInstruction("Keep vehicle stationary.");
                result.AddInstruction("Avoid airflow over the vehicle (no fans, wind).");
                break;
        }
    }

    /// <summary>
    /// Resets state (call on disconnect/reconnect).
    /// </summary>
    public void Reset()
    {
        _heartbeatCount = 0;
        _heartbeatReceived = false;
        _isArmed = false;
        _vehicleType = 0;
        _autopilot = 0;
        _logger.LogDebug("Pre-condition checker reset");
    }
}

/// <summary>
/// Result of pre-condition validation.
/// </summary>
public class CalibrationPreConditionResult
{
    public CalibrationType CalibrationType { get; set; }
    public DateTime CheckTime { get; set; }
    public bool IsValid { get; set; }
    public List<PreConditionFailureInfo> Failures { get; } = new();
    public List<PreConditionWarningInfo> Warnings { get; } = new();
    public List<string> Instructions { get; } = new();

    public void AddFailure(PreConditionFailure type, string message)
    {
        Failures.Add(new PreConditionFailureInfo { Type = type, Message = message });
    }

    public void AddWarning(PreConditionWarning type, string message)
    {
        Warnings.Add(new PreConditionWarningInfo { Type = type, Message = message });
    }

    public void AddInstruction(string instruction)
    {
        Instructions.Add(instruction);
    }

    public string GetFailuresSummary()
    {
        if (Failures.Count == 0) return "No failures";
        return string.Join("; ", Failures.Select(f => f.Message));
    }
}

public class PreConditionFailureInfo
{
    public PreConditionFailure Type { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PreConditionWarningInfo
{
    public PreConditionWarning Type { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Pre-condition failure types (HARD GATES - calibration cannot proceed).
/// </summary>
public enum PreConditionFailure
{
    NotConnected,
    HeartbeatTimeout,
    HeartbeatUnstable,
    VehicleArmed,
    VehicleMoving,
    SensorTimeout,
    ExcessiveVibration,
    EkfUsingData,
    MotorsActive,
    PropellersAttached
}

/// <summary>
/// Pre-condition warning types (calibration can proceed with caution).
/// </summary>
public enum PreConditionWarning
{
    NonArduPilotAutopilot,
    UnknownVehicleType,
    LowBattery,
    HighTemperature,
    MagneticInterference
}

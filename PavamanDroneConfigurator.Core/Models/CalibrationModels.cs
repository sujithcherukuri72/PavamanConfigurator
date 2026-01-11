using System;
using System.Collections.Generic;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Status of a calibration category or step
/// </summary>
public enum Status
{
    NotDetected,
    NotCalibrated,
    InProgress,
    Complete,
    Error
}

/// <summary>
/// Sensor category for calibration
/// </summary>
public enum SensorCategory
{
    Accelerometer,
    Compass,
    LevelHorizon,
    Pressure,
    Flow
}

/// <summary>
/// Top-level calibration category
/// </summary>
public class Category
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool Required { get; set; }
    public Status Status { get; set; }
    public List<Command> Commands { get; set; } = new();
    public List<CalibrationStepInfo> CalibrationSteps { get; set; } = new();
}

/// <summary>
/// MAVLink command definition
/// </summary>
public class Command
{
    public int CommandId { get; set; } // MAVLink command or custom id
    public string Name { get; set; } = string.Empty;
    public PayloadSchema? Schema { get; set; } // parameters
    public int TimeoutMs { get; set; } = 5000;
    public RetryPolicy? Retry { get; set; }
    public List<Precondition> Preconditions { get; set; } = new();
    public List<Postcondition> Postconditions { get; set; } = new();
}

/// <summary>
/// Calibration step with user instructions
/// </summary>
public class CalibrationStepInfo
{
    public int StepIndex { get; set; }
    public string Label { get; set; } = string.Empty; // LEVEL, LEFT, RIGHT, NOSE DOWN...
    public string InstructionText { get; set; } = string.Empty;
    public TelemetryExpectation? ExpectedTelemetry { get; set; }
    public Status StepStatus { get; set; } // Waiting / Pending / Complete / Error
}

/// <summary>
/// Defines the parameter schema for a command
/// </summary>
public class PayloadSchema
{
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Retry policy for commands
/// </summary>
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public bool ExponentialBackoff { get; set; }
}

/// <summary>
/// Precondition that must be met before executing a command
/// </summary>
public class Precondition
{
    public string Type { get; set; } = string.Empty; // e.g., "disarmed", "connected", "sensor_detected"
    public string Description { get; set; } = string.Empty;
    public Func<bool>? CheckFunction { get; set; }
}

/// <summary>
/// Postcondition to verify after command execution
/// </summary>
public class Postcondition
{
    public string Type { get; set; } = string.Empty; // e.g., "ack_received", "telemetry_valid"
    public string Description { get; set; } = string.Empty;
    public Func<bool>? CheckFunction { get; set; }
}

/// <summary>
/// Expected telemetry values for validation
/// </summary>
public class TelemetryExpectation
{
    public string MessageType { get; set; } = string.Empty; // e.g., "SCALED_IMU", "ATTITUDE"
    public Dictionary<string, object> ExpectedValues { get; set; } = new();
    public int TimeoutMs { get; set; } = 10000;
}

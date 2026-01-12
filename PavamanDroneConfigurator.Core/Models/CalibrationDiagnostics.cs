using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Comprehensive calibration diagnostics for logging and debugging.
/// Captures all information needed to diagnose calibration issues.
/// </summary>
public class CalibrationDiagnostics
{
    /// <summary>
    /// Unique identifier for this calibration session
    /// </summary>
    public Guid SessionId { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Calibration type being performed
    /// </summary>
    public CalibrationType CalibrationType { get; set; }
    
    /// <summary>
    /// When calibration started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When calibration ended (completed, failed, or cancelled)
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Total calibration duration
    /// </summary>
    public TimeSpan Duration => EndTime.HasValue 
        ? EndTime.Value - StartTime 
        : DateTime.UtcNow - StartTime;
    
    /// <summary>
    /// Current state machine state
    /// </summary>
    public CalibrationStateMachine CurrentState { get; set; } = CalibrationStateMachine.Idle;
    
    /// <summary>
    /// Final result of calibration
    /// </summary>
    public CalibrationResult Result { get; set; } = CalibrationResult.InProgress;
    
    /// <summary>
    /// All STATUSTEXT messages received during calibration
    /// </summary>
    public List<StatusTextEntry> StatusTextHistory { get; set; } = new();
    
    /// <summary>
    /// All COMMAND_ACK responses received
    /// </summary>
    public List<CommandAckEntry> CommandAckHistory { get; set; } = new();
    
    /// <summary>
    /// Diagnostic messages and warnings
    /// </summary>
    public List<DiagnosticEntry> Diagnostics { get; set; } = new();
    
    /// <summary>
    /// For accelerometer: position sampling results
    /// </summary>
    public List<AccelPositionResult> AccelPositionResults { get; set; } = new();
    
    /// <summary>
    /// For compass: 3D coverage information
    /// </summary>
    public CompassCoverageInfo? CompassCoverage { get; set; }
    
    /// <summary>
    /// Retry count for this calibration
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// Last error message if calibration failed
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Add a STATUSTEXT entry
    /// </summary>
    public void AddStatusText(byte severity, string text)
    {
        StatusTextHistory.Add(new StatusTextEntry
        {
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            Text = text
        });
    }
    
    /// <summary>
    /// Add a COMMAND_ACK entry
    /// </summary>
    public void AddCommandAck(ushort command, byte result)
    {
        CommandAckHistory.Add(new CommandAckEntry
        {
            Timestamp = DateTime.UtcNow,
            Command = command,
            Result = result
        });
    }
    
    /// <summary>
    /// Add a diagnostic entry
    /// </summary>
    public void AddDiagnostic(CalibrationDiagnosticSeverity severity, string message)
    {
        Diagnostics.Add(new DiagnosticEntry
        {
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            Message = message
        });
    }
}

/// <summary>
/// Calibration result enumeration
/// </summary>
public enum CalibrationResult
{
    /// <summary>Calibration still in progress</summary>
    InProgress,
    
    /// <summary>Calibration completed successfully (FC confirmed)</summary>
    Success,
    
    /// <summary>Calibration failed (FC reported failure)</summary>
    Failed,
    
    /// <summary>Calibration cancelled by user</summary>
    Cancelled,
    
    /// <summary>Calibration timed out</summary>
    TimedOut,
    
    /// <summary>Calibration command rejected by FC</summary>
    Rejected
}

/// <summary>
/// STATUSTEXT message entry for diagnostics
/// </summary>
public class StatusTextEntry
{
    public DateTime Timestamp { get; set; }
    public byte Severity { get; set; }
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// MAV_SEVERITY name
    /// </summary>
    public string SeverityName => Severity switch
    {
        0 => "EMERGENCY",
        1 => "ALERT",
        2 => "CRITICAL",
        3 => "ERROR",
        4 => "WARNING",
        5 => "NOTICE",
        6 => "INFO",
        7 => "DEBUG",
        _ => $"UNKNOWN({Severity})"
    };
}

/// <summary>
/// COMMAND_ACK entry for diagnostics
/// </summary>
public class CommandAckEntry
{
    public DateTime Timestamp { get; set; }
    public ushort Command { get; set; }
    public byte Result { get; set; }
    
    public string CommandName => Command switch
    {
        241 => "MAV_CMD_PREFLIGHT_CALIBRATION",
        42429 => "MAV_CMD_ACCELCAL_VEHICLE_POS",
        246 => "MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN",
        _ => $"CMD_{Command}"
    };
    
    public string ResultName => (MavResult)Result switch
    {
        MavResult.Accepted => "ACCEPTED",
        MavResult.TemporarilyRejected => "TEMPORARILY_REJECTED",
        MavResult.Denied => "DENIED",
        MavResult.Unsupported => "UNSUPPORTED",
        MavResult.Failed => "FAILED",
        MavResult.InProgress => "IN_PROGRESS",
        MavResult.Cancelled => "CANCELLED",
        _ => $"RESULT_{Result}"
    };
}

/// <summary>
/// Diagnostic message entry
/// </summary>
public class DiagnosticEntry
{
    public DateTime Timestamp { get; set; }
    public CalibrationDiagnosticSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of accelerometer position sampling
/// </summary>
public class AccelPositionResult
{
    /// <summary>
    /// Position number (1-6)
    /// </summary>
    public int Position { get; set; }
    
    /// <summary>
    /// Position name
    /// </summary>
    public string PositionName { get; set; } = string.Empty;
    
    /// <summary>
    /// When user confirmed this position
    /// </summary>
    public DateTime? UserConfirmedTime { get; set; }
    
    /// <summary>
    /// When FC accepted this position
    /// </summary>
    public DateTime? FcAcceptedTime { get; set; }
    
    /// <summary>
    /// Whether this position was accepted by FC
    /// </summary>
    public bool Accepted { get; set; }
    
    /// <summary>
    /// Number of attempts for this position
    /// </summary>
    public int Attempts { get; set; }
    
    /// <summary>
    /// FC message when position was processed
    /// </summary>
    public string? FcMessage { get; set; }
    
    /// <summary>
    /// Actual accelerometer X reading (m/s²) during validation
    /// </summary>
    public double ActualAccelX { get; set; }
    
    /// <summary>
    /// Actual accelerometer Y reading (m/s²) during validation
    /// </summary>
    public double ActualAccelY { get; set; }
    
    /// <summary>
    /// Actual accelerometer Z reading (m/s²) during validation
    /// </summary>
    public double ActualAccelZ { get; set; }
}

/// <summary>
/// Compass 3D coverage information for diagnostics
/// </summary>
public class CompassCoverageInfo
{
    /// <summary>
    /// Compass index (1-3)
    /// </summary>
    public int CompassIndex { get; set; }
    
    /// <summary>
    /// Completion percentage (0-100)
    /// </summary>
    public int CompletionPercent { get; set; }
    
    /// <summary>
    /// Calibration fitness (lower is better)
    /// </summary>
    public float Fitness { get; set; }
    
    /// <summary>
    /// Offset X result
    /// </summary>
    public float OffsetX { get; set; }
    
    /// <summary>
    /// Offset Y result
    /// </summary>
    public float OffsetY { get; set; }
    
    /// <summary>
    /// Offset Z result
    /// </summary>
    public float OffsetZ { get; set; }
    
    /// <summary>
    /// Whether calibration is complete for this compass
    /// </summary>
    public bool IsComplete { get; set; }
}

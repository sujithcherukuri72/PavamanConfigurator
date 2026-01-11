namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Status of a log download operation.
/// </summary>
public enum LogDownloadStatus
{
    Pending,
    Downloading,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Type of log file stored on the flight controller.
/// </summary>
public enum LogFileType
{
    Unknown,
    DataFlashLog,      // .bin/.log - ArduPilot DataFlash logs
    TelemetryLog,      // .tlog - MAVLink telemetry logs
    ParamFile,         // .param - Parameter files
    ScriptFile,        // .lua - Scripting files
    WaypointFile       // .waypoints - Mission files
}

/// <summary>
/// Severity levels for log analysis messages.
/// </summary>
public enum LogMessageSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Categories of issues detected during log analysis.
/// </summary>
public enum LogAnalysisCategory
{
    General,
    MotorPerformance,
    VibrationLevels,
    Vibration,
    GpsSignal,
    CompassHealth,
    BatteryHealth,
    BatteryVoltage,
    EkfStatus,
    RcSignal,
    Failsafe,
    Crash,
    FlightMode,
    Altitude,
    Attitude
}

/// <summary>
/// MAVLink FTP operation codes.
/// </summary>
public enum MavFtpOpcode : byte
{
    None = 0,
    TerminateSession = 1,
    ResetSessions = 2,
    ListDirectory = 3,
    OpenFileRO = 4,
    ReadFile = 5,
    CreateFile = 6,
    WriteFile = 7,
    RemoveFile = 8,
    CreateDirectory = 9,
    RemoveDirectory = 10,
    OpenFileWO = 11,
    TruncateFile = 12,
    Rename = 13,
    CalcFileCRC32 = 14,
    BurstReadFile = 15,
    Ack = 128,
    Nak = 129
}

/// <summary>
/// MAVLink FTP error codes.
/// </summary>
public enum MavFtpError : byte
{
    None = 0,
    Fail = 1,
    FailErrno = 2,
    InvalidDataSize = 3,
    InvalidSession = 4,
    NoSessionsAvailable = 5,
    EOF = 6,
    UnknownCommand = 7,
    FileExists = 8,
    FileProtected = 9,
    FileNotFound = 10
}

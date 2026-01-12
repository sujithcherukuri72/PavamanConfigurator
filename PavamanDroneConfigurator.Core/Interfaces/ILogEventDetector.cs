using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service for detecting flight events in log data.
/// Analyzes log for arming, failsafes, EKF issues, GPS problems, vibration warnings, etc.
/// </summary>
public interface ILogEventDetector
{
    /// <summary>
    /// Detects all events in the loaded log.
    /// </summary>
    /// <param name="progress">Progress callback (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<LogEvent>> DetectEventsAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events within a time range.
    /// </summary>
    Task<List<LogEvent>> GetEventsInRangeAsync(
        double startTime,
        double endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events by severity.
    /// </summary>
    Task<List<LogEvent>> GetEventsBySeverityAsync(
        LogEventSeverity minSeverity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events by type.
    /// </summary>
    Task<List<LogEvent>> GetEventsByTypeAsync(
        LogEventType eventType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary of detected events.
    /// </summary>
    EventSummary GetEventSummary();
}

/// <summary>
/// Represents a detected flight event.
/// </summary>
public class LogEvent
{
    public int Id { get; set; }
    public double Timestamp { get; set; }
    public string TimestampDisplay => TimeSpan.FromSeconds(Timestamp).ToString(@"hh\:mm\:ss\.fff");
    public LogEventType Type { get; set; }
    public LogEventSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    
    public string SeverityDisplay => Severity switch
    {
        LogEventSeverity.Info => "??",
        LogEventSeverity.Warning => "??",
        LogEventSeverity.Error => "?",
        LogEventSeverity.Critical => "??",
        _ => "•"
    };
}

/// <summary>
/// Event severity levels.
/// </summary>
public enum LogEventSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

/// <summary>
/// Event type categories.
/// </summary>
public enum LogEventType
{
    ModeChange,
    Arming,
    Disarming,
    Failsafe,
    EkfWarning,
    EkfError,
    GpsLoss,
    GpsGlitch,
    GpsRecovery,
    Vibration,
    Clipping,
    BatteryLow,
    BatteryCritical,
    BatteryFailsafe,
    MotorImbalance,
    CompassVariance,
    BarometerError,
    RcLoss,
    RcRecovery,
    Crash,
    Takeoff,
    Landing,
    Waypoint,
    RallyPoint,
    Fence,
    Custom
}

/// <summary>
/// Summary of detected events.
/// </summary>
public class EventSummary
{
    public int TotalEvents { get; set; }
    public int InfoCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int CriticalCount { get; set; }
    public Dictionary<LogEventType, int> EventsByType { get; set; } = new();
    public double FlightDurationSeconds { get; set; }
    public bool HasCriticalEvents => CriticalCount > 0;
    public bool HasErrors => ErrorCount > 0;
    
    /// <summary>
    /// Total count of all events.
    /// </summary>
    public int TotalCount => TotalEvents;
}

using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Mission Planner-equivalent abort monitor for calibration operations.
/// Monitors for IMMEDIATE ABORT CONDITIONS during calibration.
/// 
/// ABORT CONDITIONS (calibration aborted instantly if ANY occur):
/// - Movement detected when stationary required
/// - MAVLink heartbeat loss
/// - Sensor timeout
/// - Excessive noise, bias, or saturation
/// - User fails to comply with orientation instructions
/// 
/// Abort must include explicit failure reason.
/// </summary>
public class CalibrationAbortMonitor : IDisposable
{
    private readonly ILogger<CalibrationAbortMonitor> _logger;
    private readonly IConnectionService _connectionService;
    
    // Monitoring state
    private bool _isMonitoring;
    private CalibrationType _currentCalibrationType;
    private DateTime _lastHeartbeatTime;
    private DateTime _calibrationStartTime;
    private readonly object _lock = new();
    
    // Timers
    private System.Timers.Timer? _monitorTimer;
    
    // Thresholds
    private const int HEARTBEAT_TIMEOUT_MS = 5000;
    private const int CALIBRATION_TIMEOUT_MS = 300000; // 5 minutes max
    private const int MONITOR_INTERVAL_MS = 500;
    private const int POSITION_COMPLIANCE_TIMEOUT_MS = 60000; // 1 minute per position
    
    // Position timing
    private DateTime _positionRequestTime;
    private int _currentPositionNumber;
    
    public event EventHandler<CalibrationAbortEventArgs>? AbortTriggered;

    public CalibrationAbortMonitor(
        ILogger<CalibrationAbortMonitor> logger,
        IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
        
        _connectionService.HeartbeatReceived += OnHeartbeatReceived;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    private void OnHeartbeatReceived(object? sender, EventArgs e)
    {
        _lastHeartbeatTime = DateTime.UtcNow;
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (!connected && _isMonitoring)
        {
            TriggerAbort(CalibrationAbortReason.ConnectionLost,
                "MAVLink connection lost during calibration.");
        }
    }

    /// <summary>
    /// Starts monitoring for abort conditions.
    /// </summary>
    public void StartMonitoring(CalibrationType calibrationType)
    {
        lock (_lock)
        {
            _isMonitoring = true;
            _currentCalibrationType = calibrationType;
            _calibrationStartTime = DateTime.UtcNow;
            _lastHeartbeatTime = DateTime.UtcNow;
            _positionRequestTime = DateTime.UtcNow;
            _currentPositionNumber = 0;
        }

        _monitorTimer?.Stop();
        _monitorTimer?.Dispose();
        
        _monitorTimer = new System.Timers.Timer(MONITOR_INTERVAL_MS);
        _monitorTimer.Elapsed += OnMonitorTick;
        _monitorTimer.Start();

        _logger.LogInformation("Abort monitor started for {Type} calibration", calibrationType);
    }

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    public void StopMonitoring()
    {
        lock (_lock)
        {
            _isMonitoring = false;
        }

        _monitorTimer?.Stop();
        _monitorTimer?.Dispose();
        _monitorTimer = null;

        _logger.LogInformation("Abort monitor stopped");
    }

    /// <summary>
    /// Updates the current position being calibrated (for timeout tracking).
    /// </summary>
    public void SetCurrentPosition(int positionNumber)
    {
        lock (_lock)
        {
            _currentPositionNumber = positionNumber;
            _positionRequestTime = DateTime.UtcNow;
        }
    }

    private void OnMonitorTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isMonitoring)
            return;

        CheckAbortConditions();
    }

    private void CheckAbortConditions()
    {
        // Check 1: Heartbeat timeout
        var timeSinceHeartbeat = DateTime.UtcNow - _lastHeartbeatTime;
        if (timeSinceHeartbeat.TotalMilliseconds > HEARTBEAT_TIMEOUT_MS)
        {
            TriggerAbort(CalibrationAbortReason.HeartbeatLost,
                $"MAVLink heartbeat lost. No heartbeat for {timeSinceHeartbeat.TotalSeconds:F1}s.");
            return;
        }

        // Check 2: Overall calibration timeout
        var calibrationDuration = DateTime.UtcNow - _calibrationStartTime;
        if (calibrationDuration.TotalMilliseconds > CALIBRATION_TIMEOUT_MS)
        {
            TriggerAbort(CalibrationAbortReason.Timeout,
                $"Calibration timeout. Exceeded {CALIBRATION_TIMEOUT_MS / 60000} minute limit.");
            return;
        }

        // Check 3: Position compliance timeout (for accelerometer calibration)
        if (_currentCalibrationType == CalibrationType.Accelerometer && _currentPositionNumber > 0)
        {
            var timeSincePositionRequest = DateTime.UtcNow - _positionRequestTime;
            if (timeSincePositionRequest.TotalMilliseconds > POSITION_COMPLIANCE_TIMEOUT_MS)
            {
                TriggerAbort(CalibrationAbortReason.UserNonCompliance,
                    $"Position {_currentPositionNumber} timeout. User did not confirm position within {POSITION_COMPLIANCE_TIMEOUT_MS / 1000}s.");
                return;
            }
        }

        // Check 4: Connection state
        if (!_connectionService.IsConnected)
        {
            TriggerAbort(CalibrationAbortReason.ConnectionLost,
                "Connection lost during calibration.");
            return;
        }
    }

    private void TriggerAbort(CalibrationAbortReason reason, string message)
    {
        lock (_lock)
        {
            if (!_isMonitoring)
                return;
                
            _isMonitoring = false;
        }

        _monitorTimer?.Stop();

        _logger.LogWarning("CALIBRATION ABORT: [{Reason}] {Message}", reason, message);

        AbortTriggered?.Invoke(this, new CalibrationAbortEventArgs
        {
            Reason = reason,
            Message = message,
            CalibrationType = _currentCalibrationType,
            AbortTime = DateTime.UtcNow,
            CalibrationDuration = DateTime.UtcNow - _calibrationStartTime
        });
    }

    /// <summary>
    /// Manually trigger an abort (e.g., from STATUSTEXT indicating failure).
    /// </summary>
    public void TriggerManualAbort(string reason)
    {
        TriggerAbort(CalibrationAbortReason.FirmwareReported, reason);
    }

    public void Dispose()
    {
        StopMonitoring();
        _connectionService.HeartbeatReceived -= OnHeartbeatReceived;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}

/// <summary>
/// Event args for calibration abort events.
/// </summary>
public class CalibrationAbortEventArgs : EventArgs
{
    public CalibrationAbortReason Reason { get; set; }
    public string Message { get; set; } = string.Empty;
    public CalibrationType CalibrationType { get; set; }
    public DateTime AbortTime { get; set; }
    public TimeSpan CalibrationDuration { get; set; }
}

/// <summary>
/// Reasons for calibration abort.
/// </summary>
public enum CalibrationAbortReason
{
    /// <summary>Movement detected when stationary required</summary>
    MovementDetected,
    
    /// <summary>MAVLink heartbeat lost</summary>
    HeartbeatLost,
    
    /// <summary>MAVLink connection lost</summary>
    ConnectionLost,
    
    /// <summary>Sensor timeout</summary>
    SensorTimeout,
    
    /// <summary>Excessive noise detected</summary>
    ExcessiveNoise,
    
    /// <summary>Sensor saturation/clipping detected</summary>
    SensorSaturation,
    
    /// <summary>User did not comply with orientation instructions</summary>
    UserNonCompliance,
    
    /// <summary>Calibration exceeded maximum duration</summary>
    Timeout,
    
    /// <summary>Firmware reported failure via STATUSTEXT</summary>
    FirmwareReported,
    
    /// <summary>Vehicle became armed during calibration</summary>
    VehicleArmed,
    
    /// <summary>User cancelled calibration</summary>
    UserCancelled
}

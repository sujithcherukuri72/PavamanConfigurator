using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Infrastructure.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Detects flight events from log data.
/// Analyzes logs for arming, failsafes, EKF issues, GPS problems, vibration warnings, etc.
/// </summary>
public class LogEventDetector : ILogEventDetector
{
    private readonly ILogger<LogEventDetector> _logger;
    private DataFlashLogParser? _parser;
    private ParsedLog? _parsedLog;
    private List<LogEvent>? _cachedEvents;

    // Thresholds for event detection
    private const double VIBRATION_WARNING_THRESHOLD = 30.0;  // m/s²
    private const double VIBRATION_ERROR_THRESHOLD = 60.0;    // m/s²
    private const double CLIPPING_THRESHOLD = 100;            // clip count
    private const double BATTERY_LOW_VOLTAGE = 3.5;           // V per cell
    private const double BATTERY_CRITICAL_VOLTAGE = 3.3;      // V per cell
    private const double GPS_HDOP_WARNING = 2.0;
    private const double GPS_HDOP_ERROR = 4.0;
    private const int GPS_MIN_SATS_WARNING = 6;
    private const double EKF_VARIANCE_WARNING = 0.5;
    private const double EKF_VARIANCE_ERROR = 0.8;

    public LogEventDetector(ILogger<LogEventDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sets the parsed log data for event detection.
    /// </summary>
    public void SetLogData(DataFlashLogParser parser, ParsedLog parsedLog)
    {
        _parser = parser;
        _parsedLog = parsedLog;
        _cachedEvents = null;
    }

    public async Task<List<LogEvent>> DetectEventsAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_cachedEvents != null)
        {
            return _cachedEvents;
        }

        if (_parser == null || _parsedLog == null)
        {
            return new List<LogEvent>();
        }

        return await Task.Run(() =>
        {
            var events = new List<LogEvent>();
            var eventId = 1;

            try
            {
                progress?.Report(0);

                // Detect mode changes
                var modeEvents = DetectModeChanges(ref eventId);
                events.AddRange(modeEvents);
                progress?.Report(10);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect arming/disarming
                var armingEvents = DetectArmingEvents(ref eventId);
                events.AddRange(armingEvents);
                progress?.Report(20);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect failsafes
                var failsafeEvents = DetectFailsafeEvents(ref eventId);
                events.AddRange(failsafeEvents);
                progress?.Report(30);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect EKF issues
                var ekfEvents = DetectEkfEvents(ref eventId);
                events.AddRange(ekfEvents);
                progress?.Report(40);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect GPS issues
                var gpsEvents = DetectGpsEvents(ref eventId);
                events.AddRange(gpsEvents);
                progress?.Report(50);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect vibration issues
                var vibeEvents = DetectVibrationEvents(ref eventId);
                events.AddRange(vibeEvents);
                progress?.Report(60);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect battery issues
                var batteryEvents = DetectBatteryEvents(ref eventId);
                events.AddRange(batteryEvents);
                progress?.Report(70);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect RC issues
                var rcEvents = DetectRcEvents(ref eventId);
                events.AddRange(rcEvents);
                progress?.Report(80);

                if (cancellationToken.IsCancellationRequested) return events;

                // Detect crash/impact
                var crashEvents = DetectCrashEvents(ref eventId);
                events.AddRange(crashEvents);
                progress?.Report(90);

                // Sort by timestamp
                events = events.OrderBy(e => e.Timestamp).ToList();
                
                // Renumber
                for (int i = 0; i < events.Count; i++)
                {
                    events[i].Id = i + 1;
                }

                _cachedEvents = events;
                progress?.Report(100);

                _logger.LogInformation("Detected {Count} events in log", events.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting events");
            }

            return events;
        }, cancellationToken);
    }

    private List<LogEvent> DetectModeChanges(ref int eventId)
    {
        var events = new List<LogEvent>();

        var modeMessages = _parser!.GetMessages("MODE");
        if (modeMessages.Count == 0)
        {
            // Try MSG for mode changes
            var msgMessages = _parser.GetMessages("MSG");
            foreach (var msg in msgMessages)
            {
                var text = msg.GetStringField("Message") ?? "";
                if (text.Contains("Mode", StringComparison.OrdinalIgnoreCase))
                {
                    events.Add(new LogEvent
                    {
                        Id = eventId++,
                        Timestamp = msg.Timestamp.TotalSeconds,
                        Type = LogEventType.ModeChange,
                        Severity = LogEventSeverity.Info,
                        Title = "Mode Change",
                        Description = text
                    });
                }
            }
            return events;
        }

        string? lastMode = null;
        foreach (var msg in modeMessages)
        {
            var modeName = msg.GetStringField("Mode") ?? msg.GetStringField("ModeNum")?.ToString() ?? "Unknown";
            
            if (modeName != lastMode)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.ModeChange,
                    Severity = LogEventSeverity.Info,
                    Title = "Mode Change",
                    Description = $"Flight mode changed to {modeName}",
                    Data = { ["Mode"] = modeName }
                });
                lastMode = modeName;
            }
        }

        return events;
    }

    private List<LogEvent> DetectArmingEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        // Check EV (Event) messages for arming
        var evMessages = _parser!.GetMessages("EV");
        foreach (var msg in evMessages)
        {
            var evId = msg.GetField<double>("Id") ?? 0;
            
            // ArduPilot event IDs: 10 = Armed, 11 = Disarmed
            if ((int)evId == 10)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Arming,
                    Severity = LogEventSeverity.Info,
                    Title = "Armed",
                    Description = "Vehicle armed"
                });
            }
            else if ((int)evId == 11)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Disarming,
                    Severity = LogEventSeverity.Info,
                    Title = "Disarmed",
                    Description = "Vehicle disarmed"
                });
            }
        }

        // Also check MSG for arming text
        var msgMessages = _parser.GetMessages("MSG");
        foreach (var msg in msgMessages)
        {
            var text = msg.GetStringField("Message") ?? "";
            if (text.Contains("Arming", StringComparison.OrdinalIgnoreCase) && 
                !text.Contains("Disarm", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Arming,
                    Severity = LogEventSeverity.Info,
                    Title = "Armed",
                    Description = text
                });
            }
            else if (text.Contains("Disarm", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Disarming,
                    Severity = LogEventSeverity.Info,
                    Title = "Disarmed",
                    Description = text
                });
            }
        }

        return events;
    }

    private List<LogEvent> DetectFailsafeEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        // Check EV messages for failsafe events
        var evMessages = _parser!.GetMessages("EV");
        foreach (var msg in evMessages)
        {
            var evId = (int)(msg.GetField<double>("Id") ?? 0);
            
            // ArduPilot failsafe event IDs
            // 15 = Battery Failsafe, 17 = GPS Failsafe, 28 = Radio Failsafe, etc.
            var failsafeEvents = new Dictionary<int, (LogEventType Type, string Title)>
            {
                [15] = (LogEventType.BatteryFailsafe, "Battery Failsafe"),
                [17] = (LogEventType.Failsafe, "GPS Failsafe"),
                [28] = (LogEventType.RcLoss, "Radio Failsafe"),
                [29] = (LogEventType.BatteryFailsafe, "Battery Failsafe"),
                [36] = (LogEventType.Failsafe, "EKF Failsafe"),
                [37] = (LogEventType.Failsafe, "EKF Failsafe Cleared")
            };

            if (failsafeEvents.TryGetValue(evId, out var fsInfo))
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = fsInfo.Type,
                    Severity = LogEventSeverity.Error,
                    Title = fsInfo.Title,
                    Description = $"Failsafe triggered (Event ID: {evId})"
                });
            }
        }

        // Check MSG for failsafe text
        var msgMessages = _parser.GetMessages("MSG");
        foreach (var msg in msgMessages)
        {
            var text = msg.GetStringField("Message") ?? "";
            if (text.Contains("failsafe", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Failsafe,
                    Severity = LogEventSeverity.Error,
                    Title = "Failsafe",
                    Description = text
                });
            }
        }

        return events;
    }

    private List<LogEvent> DetectEkfEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        // Check NKF messages for EKF status
        var nkfMessages = _parser!.GetMessages("NKF4");
        if (nkfMessages.Count == 0)
            nkfMessages = _parser.GetMessages("XKF4");

        foreach (var msg in nkfMessages)
        {
            var sqErr = msg.GetField<double>("SV") ?? 0;
            
            if (sqErr > EKF_VARIANCE_ERROR)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.EkfError,
                    Severity = LogEventSeverity.Error,
                    Title = "EKF Error",
                    Description = $"EKF variance too high: {sqErr:F2}",
                    Data = { ["Variance"] = sqErr }
                });
            }
            else if (sqErr > EKF_VARIANCE_WARNING)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.EkfWarning,
                    Severity = LogEventSeverity.Warning,
                    Title = "EKF Warning",
                    Description = $"EKF variance elevated: {sqErr:F2}",
                    Data = { ["Variance"] = sqErr }
                });
            }
        }

        return events;
    }

    private List<LogEvent> DetectGpsEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        var gpsMessages = _parser!.GetMessages("GPS");
        int? lastStatus = null;

        foreach (var msg in gpsMessages)
        {
            var status = (int)(msg.GetField<double>("Status") ?? 0);
            var numSats = (int)(msg.GetField<double>("NSats") ?? msg.GetField<double>("nSat") ?? 0);
            var hdop = msg.GetField<double>("HDop") ?? msg.GetField<double>("HDOP") ?? 0;

            // GPS status: 0=NoGPS, 1=NoFix, 2=2D, 3=3D, 4=DGPS, 5=RTK Float, 6=RTK Fix
            if (status < 3 && lastStatus >= 3)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.GpsLoss,
                    Severity = LogEventSeverity.Error,
                    Title = "GPS Fix Lost",
                    Description = $"GPS fix lost (Status: {status}, Sats: {numSats})",
                    Data = { ["Status"] = status, ["Sats"] = numSats }
                });
            }
            else if (status >= 3 && lastStatus < 3 && lastStatus.HasValue)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.GpsRecovery,
                    Severity = LogEventSeverity.Info,
                    Title = "GPS Fix Recovered",
                    Description = $"GPS fix recovered (Status: {status}, Sats: {numSats})",
                    Data = { ["Status"] = status, ["Sats"] = numSats }
                });
            }

            if (hdop > GPS_HDOP_ERROR)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.GpsGlitch,
                    Severity = LogEventSeverity.Warning,
                    Title = "GPS HDOP High",
                    Description = $"GPS horizontal dilution of precision is poor: {hdop:F1}",
                    Data = { ["HDOP"] = hdop }
                });
            }

            lastStatus = status;
        }

        return events;
    }

    private List<LogEvent> DetectVibrationEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        var vibeMessages = _parser!.GetMessages("VIBE");
        
        foreach (var msg in vibeMessages)
        {
            var vibeX = Math.Abs(msg.GetField<double>("VibeX") ?? 0);
            var vibeY = Math.Abs(msg.GetField<double>("VibeY") ?? 0);
            var vibeZ = Math.Abs(msg.GetField<double>("VibeZ") ?? 0);
            var clip0 = msg.GetField<double>("Clip0") ?? 0;
            var clip1 = msg.GetField<double>("Clip1") ?? 0;
            var clip2 = msg.GetField<double>("Clip2") ?? 0;

            var maxVibe = Math.Max(vibeX, Math.Max(vibeY, vibeZ));

            if (maxVibe > VIBRATION_ERROR_THRESHOLD)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Vibration,
                    Severity = LogEventSeverity.Error,
                    Title = "High Vibration",
                    Description = $"Excessive vibration detected: X={vibeX:F1}, Y={vibeY:F1}, Z={vibeZ:F1} m/s²",
                    Data = { ["VibeX"] = vibeX, ["VibeY"] = vibeY, ["VibeZ"] = vibeZ }
                });
            }
            else if (maxVibe > VIBRATION_WARNING_THRESHOLD)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Vibration,
                    Severity = LogEventSeverity.Warning,
                    Title = "Elevated Vibration",
                    Description = $"Vibration elevated: X={vibeX:F1}, Y={vibeY:F1}, Z={vibeZ:F1} m/s²",
                    Data = { ["VibeX"] = vibeX, ["VibeY"] = vibeY, ["VibeZ"] = vibeZ }
                });
            }

            var totalClips = clip0 + clip1 + clip2;
            if (totalClips > CLIPPING_THRESHOLD)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Clipping,
                    Severity = LogEventSeverity.Warning,
                    Title = "Accelerometer Clipping",
                    Description = $"Accelerometer clipping detected: {totalClips:F0} clips",
                    Data = { ["Clips"] = totalClips }
                });
            }
        }

        return events;
    }

    private List<LogEvent> DetectBatteryEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        var batMessages = _parser!.GetMessages("BAT");
        if (batMessages.Count == 0)
            batMessages = _parser.GetMessages("CURR");

        bool lowVoltageReported = false;
        bool criticalVoltageReported = false;

        foreach (var msg in batMessages)
        {
            var voltage = msg.GetField<double>("Volt") ?? 0;
            var cellCount = EstimateCellCount(voltage);
            
            if (cellCount > 0)
            {
                var voltPerCell = voltage / cellCount;

                if (voltPerCell < BATTERY_CRITICAL_VOLTAGE && !criticalVoltageReported)
                {
                    events.Add(new LogEvent
                    {
                        Id = eventId++,
                        Timestamp = msg.Timestamp.TotalSeconds,
                        Type = LogEventType.BatteryCritical,
                        Severity = LogEventSeverity.Critical,
                        Title = "Battery Critical",
                        Description = $"Battery voltage critical: {voltage:F1}V ({voltPerCell:F2}V/cell)",
                        Data = { ["Voltage"] = voltage, ["VoltPerCell"] = voltPerCell }
                    });
                    criticalVoltageReported = true;
                }
                else if (voltPerCell < BATTERY_LOW_VOLTAGE && !lowVoltageReported)
                {
                    events.Add(new LogEvent
                    {
                        Id = eventId++,
                        Timestamp = msg.Timestamp.TotalSeconds,
                        Type = LogEventType.BatteryLow,
                        Severity = LogEventSeverity.Warning,
                        Title = "Battery Low",
                        Description = $"Battery voltage low: {voltage:F1}V ({voltPerCell:F2}V/cell)",
                        Data = { ["Voltage"] = voltage, ["VoltPerCell"] = voltPerCell }
                    });
                    lowVoltageReported = true;
                }
            }
        }

        return events;
    }

    private List<LogEvent> DetectRcEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        var rcMessages = _parser!.GetMessages("RCIN");
        ushort? lastRssi = null;

        foreach (var msg in rcMessages)
        {
            var rssi = (ushort)(msg.GetField<double>("RSSI") ?? 255);
            
            if (rssi < 50 && (lastRssi == null || lastRssi >= 50))
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.RcLoss,
                    Severity = LogEventSeverity.Warning,
                    Title = "RC Signal Weak",
                    Description = $"RC signal strength low: RSSI={rssi}",
                    Data = { ["RSSI"] = rssi }
                });
            }
            else if (rssi >= 50 && lastRssi < 50)
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.RcRecovery,
                    Severity = LogEventSeverity.Info,
                    Title = "RC Signal Recovered",
                    Description = $"RC signal recovered: RSSI={rssi}",
                    Data = { ["RSSI"] = rssi }
                });
            }

            lastRssi = rssi;
        }

        return events;
    }

    private List<LogEvent> DetectCrashEvents(ref int eventId)
    {
        var events = new List<LogEvent>();

        // Check for crash detection in MSG
        var msgMessages = _parser!.GetMessages("MSG");
        foreach (var msg in msgMessages)
        {
            var text = msg.GetStringField("Message") ?? "";
            if (text.Contains("crash", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("impact", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new LogEvent
                {
                    Id = eventId++,
                    Timestamp = msg.Timestamp.TotalSeconds,
                    Type = LogEventType.Crash,
                    Severity = LogEventSeverity.Critical,
                    Title = "Crash Detected",
                    Description = text
                });
            }
        }

        return events;
    }

    private static int EstimateCellCount(double voltage)
    {
        // Estimate cell count based on voltage
        if (voltage < 5) return 1;       // 1S
        if (voltage < 9) return 2;       // 2S
        if (voltage < 13) return 3;      // 3S
        if (voltage < 17) return 4;      // 4S
        if (voltage < 22) return 5;      // 5S
        if (voltage < 26) return 6;      // 6S
        if (voltage < 30) return 7;      // 7S
        if (voltage < 35) return 8;      // 8S
        return 0;
    }

    public Task<List<LogEvent>> GetEventsInRangeAsync(
        double startTime,
        double endTime,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            (_cachedEvents ?? new List<LogEvent>())
            .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
            .ToList()
        );
    }

    public Task<List<LogEvent>> GetEventsBySeverityAsync(
        LogEventSeverity minSeverity,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            (_cachedEvents ?? new List<LogEvent>())
            .Where(e => e.Severity >= minSeverity)
            .ToList()
        );
    }

    public Task<List<LogEvent>> GetEventsByTypeAsync(
        LogEventType eventType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            (_cachedEvents ?? new List<LogEvent>())
            .Where(e => e.Type == eventType)
            .ToList()
        );
    }

    public EventSummary GetEventSummary()
    {
        var events = _cachedEvents ?? new List<LogEvent>();
        
        return new EventSummary
        {
            TotalEvents = events.Count,
            InfoCount = events.Count(e => e.Severity == LogEventSeverity.Info),
            WarningCount = events.Count(e => e.Severity == LogEventSeverity.Warning),
            ErrorCount = events.Count(e => e.Severity == LogEventSeverity.Error),
            CriticalCount = events.Count(e => e.Severity == LogEventSeverity.Critical),
            EventsByType = events.GroupBy(e => e.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            FlightDurationSeconds = _parsedLog?.Duration.TotalSeconds ?? 0
        };
    }
}

using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Flight mode configuration settings for 6 flight mode slots.
/// Maps to ArduPilot FLTMODE parameters.
/// </summary>
public class FlightModeSettings
{
    /// <summary>
    /// RC channel used for flight mode selection (FLTMODE_CH)
    /// Default is Channel 5
    /// </summary>
    public FlightModeChannel ModeChannel { get; set; } = FlightModeChannel.Channel5;

    /// <summary>
    /// Flight Mode 1 - PWM 0-1230 (FLTMODE1)
    /// </summary>
    public FlightMode Mode1 { get; set; } = FlightMode.Stabilize;

    /// <summary>
    /// Flight Mode 2 - PWM 1231-1360 (FLTMODE2)
    /// </summary>
    public FlightMode Mode2 { get; set; } = FlightMode.Stabilize;

    /// <summary>
    /// Flight Mode 3 - PWM 1361-1490 (FLTMODE3)
    /// </summary>
    public FlightMode Mode3 { get; set; } = FlightMode.Stabilize;

    /// <summary>
    /// Flight Mode 4 - PWM 1491-1620 (FLTMODE4)
    /// </summary>
    public FlightMode Mode4 { get; set; } = FlightMode.Stabilize;

    /// <summary>
    /// Flight Mode 5 - PWM 1621-1749 (FLTMODE5)
    /// </summary>
    public FlightMode Mode5 { get; set; } = FlightMode.Stabilize;

    /// <summary>
    /// Flight Mode 6 - PWM 1750+ (FLTMODE6)
    /// </summary>
    public FlightMode Mode6 { get; set; } = FlightMode.Stabilize;

    /// <summary>
    /// Simple Mode setting for Mode 1
    /// </summary>
    public SimpleMode Simple1 { get; set; } = SimpleMode.Off;

    /// <summary>
    /// Simple Mode setting for Mode 2
    /// </summary>
    public SimpleMode Simple2 { get; set; } = SimpleMode.Off;

    /// <summary>
    /// Simple Mode setting for Mode 3
    /// </summary>
    public SimpleMode Simple3 { get; set; } = SimpleMode.Off;

    /// <summary>
    /// Simple Mode setting for Mode 4
    /// </summary>
    public SimpleMode Simple4 { get; set; } = SimpleMode.Off;

    /// <summary>
    /// Simple Mode setting for Mode 5
    /// </summary>
    public SimpleMode Simple5 { get; set; } = SimpleMode.Off;

    /// <summary>
    /// Simple Mode setting for Mode 6
    /// </summary>
    public SimpleMode Simple6 { get; set; } = SimpleMode.Off;

    /// <summary>
    /// Current active flight mode reported by vehicle
    /// </summary>
    public FlightMode? CurrentMode { get; set; }

    /// <summary>
    /// Current PWM value on the mode channel
    /// </summary>
    public int CurrentPwm { get; set; }

    /// <summary>
    /// Get the PWM range description for a mode slot
    /// </summary>
    public static string GetPwmRange(int modeSlot) => modeSlot switch
    {
        1 => "PWM 0 - 1230",
        2 => "PWM 1231 - 1360",
        3 => "PWM 1361 - 1490",
        4 => "PWM 1491 - 1620",
        5 => "PWM 1621 - 1749",
        6 => "PWM 1750+",
        _ => "Unknown"
    };

    /// <summary>
    /// Determine which mode slot is active based on PWM value
    /// </summary>
    public static int GetActiveModeSlot(int pwm)
    {
        if (pwm <= 1230) return 1;
        if (pwm <= 1360) return 2;
        if (pwm <= 1490) return 3;
        if (pwm <= 1620) return 4;
        if (pwm <= 1749) return 5;
        return 6;
    }
}

/// <summary>
/// Information about a flight mode for display purposes
/// </summary>
public class FlightModeInfo
{
    public FlightMode Mode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresGps { get; set; }
    public bool IsAutonomous { get; set; }
    public bool IsSafeForBeginners { get; set; }

    /// <summary>
    /// Get detailed information about a flight mode
    /// </summary>
    public static FlightModeInfo GetModeInfo(FlightMode mode) => mode switch
    {
        FlightMode.Stabilize => new FlightModeInfo
        {
            Mode = mode,
            Name = "Stabilize",
            Description = "Manual control with self-leveling. Pilot controls roll, pitch, yaw and throttle.",
            RequiresGps = false,
            IsAutonomous = false,
            IsSafeForBeginners = true
        },
        FlightMode.Acro => new FlightModeInfo
        {
            Mode = mode,
            Name = "Acro",
            Description = "Rate-controlled mode for aerobatic maneuvers. No self-leveling.",
            RequiresGps = false,
            IsAutonomous = false,
            IsSafeForBeginners = false
        },
        FlightMode.AltHold => new FlightModeInfo
        {
            Mode = mode,
            Name = "Altitude Hold",
            Description = "Maintains current altitude automatically. Pilot controls roll, pitch, yaw.",
            RequiresGps = false,
            IsAutonomous = false,
            IsSafeForBeginners = true
        },
        FlightMode.Auto => new FlightModeInfo
        {
            Mode = mode,
            Name = "Auto",
            Description = "Executes pre-programmed mission waypoints automatically.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        FlightMode.Guided => new FlightModeInfo
        {
            Mode = mode,
            Name = "Guided",
            Description = "Flies to locations commanded by ground station or companion computer.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        FlightMode.Loiter => new FlightModeInfo
        {
            Mode = mode,
            Name = "Loiter",
            Description = "GPS position hold. Vehicle holds position and altitude automatically.",
            RequiresGps = true,
            IsAutonomous = false,
            IsSafeForBeginners = true
        },
        FlightMode.RTL => new FlightModeInfo
        {
            Mode = mode,
            Name = "Return to Launch",
            Description = "Automatically returns to home location and lands.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = true
        },
        FlightMode.Circle => new FlightModeInfo
        {
            Mode = mode,
            Name = "Circle",
            Description = "Circles around a point of interest at current altitude.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        FlightMode.Land => new FlightModeInfo
        {
            Mode = mode,
            Name = "Land",
            Description = "Descends and lands at current location.",
            RequiresGps = false,
            IsAutonomous = true,
            IsSafeForBeginners = true
        },
        FlightMode.Drift => new FlightModeInfo
        {
            Mode = mode,
            Name = "Drift",
            Description = "Like stabilize but with coordinated turns like a car.",
            RequiresGps = false,
            IsAutonomous = false,
            IsSafeForBeginners = false
        },
        FlightMode.Sport => new FlightModeInfo
        {
            Mode = mode,
            Name = "Sport",
            Description = "Similar to stabilize but with higher rate limits for faster flying.",
            RequiresGps = false,
            IsAutonomous = false,
            IsSafeForBeginners = false
        },
        FlightMode.Flip => new FlightModeInfo
        {
            Mode = mode,
            Name = "Flip",
            Description = "Performs an automated flip maneuver.",
            RequiresGps = false,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        FlightMode.AutoTune => new FlightModeInfo
        {
            Mode = mode,
            Name = "AutoTune",
            Description = "Automatically tunes roll and pitch PIDs for optimal flight.",
            RequiresGps = false,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        FlightMode.PosHold => new FlightModeInfo
        {
            Mode = mode,
            Name = "Position Hold",
            Description = "GPS position hold with lean angle based position control.",
            RequiresGps = true,
            IsAutonomous = false,
            IsSafeForBeginners = true
        },
        FlightMode.Brake => new FlightModeInfo
        {
            Mode = mode,
            Name = "Brake",
            Description = "Stops vehicle and holds position. Good for emergency stop.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = true
        },
        FlightMode.Throw => new FlightModeInfo
        {
            Mode = mode,
            Name = "Throw",
            Description = "Allows launching by throwing the vehicle into the air.",
            RequiresGps = false,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        FlightMode.SmartRTL => new FlightModeInfo
        {
            Mode = mode,
            Name = "Smart RTL",
            Description = "Returns home following the path it flew, avoiding obstacles.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = true
        },
        FlightMode.FlowHold => new FlightModeInfo
        {
            Mode = mode,
            Name = "Flow Hold",
            Description = "Position hold using optical flow sensor instead of GPS.",
            RequiresGps = false,
            IsAutonomous = false,
            IsSafeForBeginners = false
        },
        FlightMode.Follow => new FlightModeInfo
        {
            Mode = mode,
            Name = "Follow",
            Description = "Follows another vehicle or ground station.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        FlightMode.ZigZag => new FlightModeInfo
        {
            Mode = mode,
            Name = "ZigZag",
            Description = "Flies an automated zigzag pattern between waypoints.",
            RequiresGps = true,
            IsAutonomous = true,
            IsSafeForBeginners = false
        },
        _ => new FlightModeInfo
        {
            Mode = mode,
            Name = mode.ToString(),
            Description = "Flight mode",
            RequiresGps = false,
            IsAutonomous = false,
            IsSafeForBeginners = false
        }
    };
}

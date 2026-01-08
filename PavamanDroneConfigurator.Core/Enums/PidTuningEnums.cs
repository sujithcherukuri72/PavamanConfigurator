namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Axis selection for PID tuning
/// </summary>
public enum TuningAxis
{
    Roll = 0,
    Pitch = 1,
    Yaw = 2
}

/// <summary>
/// AutoTune axis bitmask values for AUTOTUNE_AXES parameter
/// </summary>
[Flags]
public enum AutoTuneAxes
{
    None = 0,
    Roll = 1,      // Bit 0
    Pitch = 2,     // Bit 1
    Yaw = 4        // Bit 2
}

/// <summary>
/// RC Channel options for AutoTune switch assignment
/// Maps to RC_OPTIONS parameter values
/// </summary>
public enum AutoTuneChannel
{
    None = 0,
    Channel5 = 5,
    Channel6 = 6,
    Channel7 = 7,
    Channel8 = 8,
    Channel9 = 9,
    Channel10 = 10,
    Channel11 = 11,
    Channel12 = 12
}

/// <summary>
/// In-flight tuning parameter options for TUNE parameter
/// ArduCopter tuning knob options
/// </summary>
public enum InFlightTuningOption
{
    None = 0,
    StabilizeRollPitchKp = 1,
    StabilizeYawKp = 3,
    RatePitchRollKp = 4,
    RatePitchRollKi = 5,
    RateYawKp = 6,
    ThrottleRateKp = 7,
    WpSpeed = 10,
    LoiterPosKp = 12,
    HelvibFFD = 13,
    AltHoldKp = 14,
    ThrottleAccelKp = 15,
    ThrottleAccelKi = 16,
    ThrottleAccelKd = 17,
    LoiterRateKp = 22,
    LoiterRateKi = 28,
    LoiterRateKd = 29,
    RatePitchRollKd = 21,
    RateYawKd = 26,
    RateYawFilter = 55,
    RatePitchRollFilter = 50,
    AngleMax = 38,
    PosXYP = 39,
    VelXYP = 41,
    VelXYI = 42,
    VelXYD = 43,
    PosZP = 44,
    VelZP = 45,
    AccelZP = 46,
    AccelZI = 47,
    AccelZD = 48,
    Declination = 38,
    CircleRate = 39,
    RangefinderGain = 41,
    AcroYawP = 49,
    SystemIdMagnitude = 54
}

/// <summary>
/// AutoTune aggressiveness levels
/// </summary>
public enum AutoTuneAggressiveness
{
    Low = 0,      // Conservative tuning
    Medium = 1,   // Default tuning
    High = 2      // Aggressive tuning
}

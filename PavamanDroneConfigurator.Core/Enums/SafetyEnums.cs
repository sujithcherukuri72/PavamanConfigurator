namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Motor emergency stop behavior matching ArduPilot MOT_SAFE_DISARM
/// </summary>
public enum MotorSafetyDisarm
{
    /// <summary>Disarm only when landed</summary>
    DisarmWhenLanded = 0,
    
    /// <summary>Disarm anytime (dangerous)</summary>
    DisarmAnytime = 1
}

/// <summary>
/// EKF (Extended Kalman Filter) failsafe actions matching ArduPilot FS_EKF_ACTION
/// </summary>
public enum EKFFailsafeAction
{
    /// <summary>Disabled</summary>
    Disabled = 0,
    
    /// <summary>Land</summary>
    Land = 1,
    
    /// <summary>AltHold mode</summary>
    AltHold = 2,
    
    /// <summary>Land even in stabilize mode</summary>
    LandEvenInStabilize = 3
}

/// <summary>
/// Vibration failsafe actions matching ArduPilot FS_VIBE_ENABLE
/// </summary>
public enum VibrationFailsafeAction
{
    /// <summary>Disabled</summary>
    Disabled = 0,
    
    /// <summary>Warn only</summary>
    WarnOnly = 1,
    
    /// <summary>Land</summary>
    Land = 2
}

/// <summary>
/// Crash check actions matching ArduPilot FS_CRASH_CHECK
/// </summary>
public enum CrashCheckAction
{
    /// <summary>Disabled</summary>
    Disabled = 0,
    
    /// <summary>Disarm on crash</summary>
    Disarm = 1
}

/// <summary>
/// Parachute enable options matching ArduPilot CHUTE_ENABLED
/// </summary>
public enum ParachuteEnabled
{
    /// <summary>Disabled</summary>
    Disabled = 0,
    
    /// <summary>Enabled</summary>
    Enabled = 1,
    
    /// <summary>Enabled with release on land</summary>
    EnabledWithRelease = 2
}

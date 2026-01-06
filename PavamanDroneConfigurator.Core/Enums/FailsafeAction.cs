namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Comprehensive failsafe actions for PDRL compliance.
/// Values match ArduPilot MAVLink parameter definitions.
/// </summary>
public enum FailsafeAction
{
    /// <summary>No action taken</summary>
    Disabled = 0,
    
    /// <summary>Land immediately at current position</summary>
    Land = 1,
    
    /// <summary>Return to launch point</summary>
    RTL = 2,
    
    /// <summary>Smart RTL - retrace path if possible, else RTL</summary>
    SmartRTL = 3,
    
    /// <summary>Smart RTL or Land if Smart RTL not available</summary>
    SmartRTLOrLand = 4,
    
    /// <summary>Immediately terminate flight (motors stop)</summary>
    Terminate = 5,
    
    /// <summary>Auto land at current position</summary>
    AutoLand = 6,
    
    /// <summary>Brake and hold position (requires GPS)</summary>
    Brake = 7,
    
    /// <summary>Continue with current mission</summary>
    ContinueMission = 8,
    
    /// <summary>Switch to Auto mode and continue mission</summary>
    AutoMode = 9,
    
    /// <summary>Switch to Loiter mode</summary>
    Loiter = 10,
    
    /// <summary>Disarm motors immediately</summary>
    Disarm = 11
}

/// <summary>
/// Battery failsafe actions matching ArduPilot BATT_FS_LOW_ACT / BATT_FS_CRT_ACT
/// </summary>
public enum BatteryFailsafeAction
{
    /// <summary>No action</summary>
    Disabled = 0,
    
    /// <summary>Land immediately</summary>
    Land = 1,
    
    /// <summary>Return to launch</summary>
    RTL = 2,
    
    /// <summary>Smart RTL or Land</summary>
    SmartRTLOrLand = 3,
    
    /// <summary>Smart RTL or RTL</summary>
    SmartRTLOrRTL = 4,
    
    /// <summary>Terminate flight</summary>
    Terminate = 5
}

/// <summary>
/// RC/Throttle failsafe actions matching ArduPilot FS_THR_ENABLE
/// </summary>
public enum RCFailsafeAction
{
    /// <summary>Disabled</summary>
    Disabled = 0,
    
    /// <summary>Always RTL</summary>
    AlwaysRTL = 1,
    
    /// <summary>Continue with mission in Auto mode</summary>
    ContinueMissionInAuto = 2,
    
    /// <summary>Always land</summary>
    AlwaysLand = 3,
    
    /// <summary>Smart RTL or RTL</summary>
    SmartRTLOrRTL = 4,
    
    /// <summary>Smart RTL or Land</summary>
    SmartRTLOrLand = 5,
    
    /// <summary>Terminate</summary>
    Terminate = 6
}

/// <summary>
/// GCS failsafe actions matching ArduPilot FS_GCS_ENABLE
/// </summary>
public enum GCSFailsafeAction
{
    /// <summary>Disabled</summary>
    Disabled = 0,
    
    /// <summary>RTL</summary>
    RTL = 1,
    
    /// <summary>Continue with mission in Auto mode</summary>
    ContinueMissionInAuto = 2,
    
    /// <summary>Smart RTL or RTL</summary>
    SmartRTLOrRTL = 3,
    
    /// <summary>Smart RTL or Land</summary>
    SmartRTLOrLand = 4,
    
    /// <summary>Terminate</summary>
    Terminate = 5
}

/// <summary>
/// Geofence breach actions matching ArduPilot FENCE_ACTION
/// </summary>
public enum FenceAction
{
    /// <summary>Report only - no action</summary>
    ReportOnly = 0,
    
    /// <summary>RTL or Land</summary>
    RTLOrLand = 1,
    
    /// <summary>Always Land</summary>
    Land = 2,
    
    /// <summary>Smart RTL or RTL or Land</summary>
    SmartRTLOrRTLOrLand = 3,
    
    /// <summary>Brake or Land</summary>
    BrakeOrLand = 4,
    
    /// <summary>Smart RTL or Land</summary>
    SmartRTLOrLand = 5,
    
    /// <summary>Terminate</summary>
    Terminate = 6
}

/// <summary>
/// Fence types matching ArduPilot FENCE_TYPE bitmask
/// </summary>
[Flags]
public enum FenceType
{
    /// <summary>No fence</summary>
    None = 0,
    
    /// <summary>Maximum altitude fence</summary>
    AltitudeMax = 1,
    
    /// <summary>Circular fence around home</summary>
    Circle = 2,
    
    /// <summary>Polygon fence</summary>
    Polygon = 4,
    
    /// <summary>Minimum altitude fence</summary>
    AltitudeMin = 8,
    
    /// <summary>All fence types enabled</summary>
    All = AltitudeMax | Circle | Polygon | AltitudeMin
}

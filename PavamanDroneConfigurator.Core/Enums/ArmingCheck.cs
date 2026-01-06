namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Arming check bitmask flags matching ArduPilot ARMING_CHECK parameter.
/// PDRL requires specific checks to be enabled before flight.
/// </summary>
[Flags]
public enum ArmingCheck
{
    /// <summary>No checks</summary>
    None = 0,
    
    /// <summary>All checks enabled</summary>
    All = 1,
    
    /// <summary>Barometer check</summary>
    Barometer = 2,
    
    /// <summary>Compass/Magnetometer check</summary>
    Compass = 4,
    
    /// <summary>GPS lock check</summary>
    GPS = 8,
    
    /// <summary>INS (Inertial Navigation System) check</summary>
    INS = 16,
    
    /// <summary>Parameters check</summary>
    Parameters = 32,
    
    /// <summary>RC (Radio Control) receiver check</summary>
    RC = 64,
    
    /// <summary>Board voltage check</summary>
    Voltage = 128,
    
    /// <summary>Battery level check</summary>
    Battery = 256,
    
    /// <summary>Airspeed sensor check (fixed wing)</summary>
    Airspeed = 512,
    
    /// <summary>Logging available check</summary>
    Logging = 1024,
    
    /// <summary>Safety switch check</summary>
    SafetySwitch = 2048,
    
    /// <summary>GPS configuration check</summary>
    GPSConfig = 4096,
    
    /// <summary>System check</summary>
    System = 8192,
    
    /// <summary>Mission check</summary>
    Mission = 16384,
    
    /// <summary>Rangefinder check</summary>
    Rangefinder = 32768,
    
    /// <summary>Camera check</summary>
    Camera = 65536,
    
    /// <summary>Aux authorization check</summary>
    AuxAuth = 131072,
    
    /// <summary>Visual odometry check</summary>
    VisualOdometry = 262144,
    
    /// <summary>FFT (vibration analysis) check</summary>
    FFT = 524288,
    
    /// <summary>PDRL recommended minimum checks</summary>
    PDRLMinimum = Barometer | Compass | GPS | INS | RC | Battery | Parameters
}

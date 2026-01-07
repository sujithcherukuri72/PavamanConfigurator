namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// ArduPilot Copter flight modes.
/// Values correspond to MAVLink COPTER_MODE enum.
/// </summary>
public enum FlightMode
{
    /// <summary>Stabilize - Manual control with self-leveling</summary>
    Stabilize = 0,
    
    /// <summary>Acro - Rate controlled mode for aerobatics</summary>
    Acro = 1,
    
    /// <summary>AltHold - Altitude hold with manual throttle</summary>
    AltHold = 2,
    
    /// <summary>Auto - Automated waypoint navigation</summary>
    Auto = 3,
    
    /// <summary>Guided - External computer control</summary>
    Guided = 4,
    
    /// <summary>Loiter - GPS position hold</summary>
    Loiter = 5,
    
    /// <summary>RTL - Return to launch point</summary>
    RTL = 6,
    
    /// <summary>Circle - Circle around a point</summary>
    Circle = 7,
    
    /// <summary>Land - Automated landing</summary>
    Land = 9,
    
    /// <summary>Drift - Coordinated turn mode</summary>
    Drift = 11,
    
    /// <summary>Sport - Sport mode with higher rates</summary>
    Sport = 13,
    
    /// <summary>Flip - Automated flip maneuver</summary>
    Flip = 14,
    
    /// <summary>AutoTune - Automatic PID tuning</summary>
    AutoTune = 15,
    
    /// <summary>PosHold - Position hold with lean angle control</summary>
    PosHold = 16,
    
    /// <summary>Brake - Stop and hold position</summary>
    Brake = 17,
    
    /// <summary>Throw - Launch by throwing</summary>
    Throw = 18,
    
    /// <summary>Avoid ADSB - Avoid other aircraft</summary>
    AvoidADSB = 19,
    
    /// <summary>Guided NoGPS - Guided without GPS</summary>
    GuidedNoGPS = 20,
    
    /// <summary>Smart RTL - Return via recorded path</summary>
    SmartRTL = 21,
    
    /// <summary>FlowHold - Optical flow position hold</summary>
    FlowHold = 22,
    
    /// <summary>Follow - Follow another vehicle</summary>
    Follow = 23,
    
    /// <summary>ZigZag - Automated zigzag pattern</summary>
    ZigZag = 24,
    
    /// <summary>SystemID - System identification mode</summary>
    SystemID = 25,
    
    /// <summary>Heli Autorotate - Helicopter autorotation</summary>
    HeliAutorotate = 26,
    
    /// <summary>Auto RTL - Auto then RTL</summary>
    AutoRTL = 27
}

/// <summary>
/// RC Channel options for flight mode switch
/// </summary>
public enum FlightModeChannel
{
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
/// Simple mode options
/// </summary>
public enum SimpleMode
{
    /// <summary>Simple mode disabled</summary>
    Off = 0,
    
    /// <summary>Simple mode - heading relative to home</summary>
    Simple = 1,
    
    /// <summary>Super Simple - heading relative to home, updates during flight</summary>
    SuperSimple = 2
}

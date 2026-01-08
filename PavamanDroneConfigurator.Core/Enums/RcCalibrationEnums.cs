namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// RC input channel indices (1-16)
/// ArduPilot supports up to 16 RC input channels
/// </summary>
public enum RcChannel
{
    Channel1 = 1,
    Channel2 = 2,
    Channel3 = 3,
    Channel4 = 4,
    Channel5 = 5,
    Channel6 = 6,
    Channel7 = 7,
    Channel8 = 8,
    Channel9 = 9,
    Channel10 = 10,
    Channel11 = 11,
    Channel12 = 12,
    Channel13 = 13,
    Channel14 = 14,
    Channel15 = 15,
    Channel16 = 16
}

/// <summary>
/// RC calibration states for the calibration process
/// </summary>
public enum RcCalibrationState
{
    /// <summary>Not calibrating, ready to start</summary>
    Idle,
    
    /// <summary>Waiting for user to start calibration</summary>
    WaitingToStart,
    
    /// <summary>Collecting stick movement data</summary>
    CollectingMinMax,
    
    /// <summary>Waiting for sticks to be centered</summary>
    WaitingForCenter,
    
    /// <summary>Calibration completed successfully</summary>
    Completed,
    
    /// <summary>Calibration failed</summary>
    Failed,
    
    /// <summary>Calibration cancelled by user</summary>
    Cancelled
}

/// <summary>
/// Attitude control function assignment
/// Maps to ArduPilot RCMAP_ parameters
/// </summary>
public enum AttitudeFunction
{
    /// <summary>Roll control (RCMAP_ROLL)</summary>
    Roll,
    
    /// <summary>Pitch control (RCMAP_PITCH)</summary>
    Pitch,
    
    /// <summary>Throttle control (RCMAP_THROTTLE)</summary>
    Throttle,
    
    /// <summary>Yaw control (RCMAP_YAW)</summary>
    Yaw
}

/// <summary>
/// RC channel options for auxiliary functions
/// Matches ArduPilot RCx_OPTION parameter values
/// </summary>
public enum RcChannelOption
{
    /// <summary>Do nothing</summary>
    DoNothing = 0,
    
    /// <summary>Flip mode</summary>
    Flip = 2,
    
    /// <summary>Simple mode</summary>
    SimpleMode = 5,
    
    /// <summary>RTL mode</summary>
    RTL = 6,
    
    /// <summary>Save trim</summary>
    SaveTrim = 7,
    
    /// <summary>Save waypoint</summary>
    SaveWaypoint = 9,
    
    /// <summary>Camera trigger</summary>
    CameraTrigger = 10,
    
    /// <summary>Rangefinder</summary>
    Rangefinder = 11,
    
    /// <summary>Fence enable</summary>
    Fence = 12,
    
    /// <summary>Super simple mode</summary>
    SuperSimpleMode = 14,
    
    /// <summary>Acro trainer</summary>
    AcroTrainer = 15,
    
    /// <summary>Auto tune</summary>
    AutoTune = 17,
    
    /// <summary>Land mode</summary>
    Land = 18,
    
    /// <summary>Gripper</summary>
    Gripper = 19,
    
    /// <summary>Parachute enable</summary>
    ParachuteEnable = 21,
    
    /// <summary>Parachute release</summary>
    ParachuteRelease = 22,
    
    /// <summary>Mission reset</summary>
    MissionReset = 24,
    
    /// <summary>Attitude hold</summary>
    AttitudeHold = 25,
    
    /// <summary>PosHold mode</summary>
    PosHold = 26,
    
    /// <summary>AltHold mode</summary>
    AltHold = 27,
    
    /// <summary>Loiter mode</summary>
    Loiter = 28,
    
    /// <summary>Motor interlock</summary>
    MotorInterlock = 32,
    
    /// <summary>Brake mode</summary>
    Brake = 33,
    
    /// <summary>Throw mode</summary>
    Throw = 35,
    
    /// <summary>GPS disable</summary>
    GpsDisable = 37,
    
    /// <summary>Motor emergency stop</summary>
    MotorEStop = 38,
    
    /// <summary>Motor emergency stop (non-latching)</summary>
    MotorEStopNonLatching = 39,
    
    /// <summary>Stabilize mode</summary>
    Stabilize = 40,
    
    /// <summary>Arm/Disarm toggle</summary>
    ArmDisarm = 41,
    
    /// <summary>Smart RTL mode</summary>
    SmartRTL = 42,
    
    /// <summary>Inverted flight</summary>
    InvertedFlight = 43,
    
    /// <summary>Winch enable</summary>
    WinchEnable = 44,
    
    /// <summary>Winch control</summary>
    WinchControl = 45,
    
    /// <summary>Clear waypoints</summary>
    ClearWaypoints = 56,
    
    /// <summary>Zigzag mode</summary>
    ZigZag = 58,
    
    /// <summary>Surface tracking up/down</summary>
    SurfaceTrackingUpDown = 65,
    
    /// <summary>Standby mode</summary>
    Standby = 67,
    
    /// <summary>Generator emergency stop</summary>
    GeneratorEStop = 85,
    
    /// <summary>Auto mode</summary>
    Auto = 100,
    
    /// <summary>Guided mode</summary>
    Guided = 101,
    
    /// <summary>Circle mode</summary>
    Circle = 102,
    
    /// <summary>Drift mode</summary>
    Drift = 103,
    
    /// <summary>Sport mode</summary>
    Sport = 104,
    
    /// <summary>Follow mode</summary>
    Follow = 105,
    
    /// <summary>Zigzag save waypoint</summary>
    ZigZagSaveWaypoint = 106,
    
    /// <summary>Acro mode</summary>
    Acro = 107,
    
    /// <summary>System identification</summary>
    SystemId = 112,
    
    /// <summary>AutoRotate mode</summary>
    AutoRotate = 117,
    
    /// <summary>Turtle mode</summary>
    Turtle = 132,
    
    /// <summary>FlowHold mode</summary>
    FlowHold = 133,
    
    /// <summary>Kill switch</summary>
    KillSwitch = 153,
    
    /// <summary>Script aux function 1</summary>
    ScriptAux1 = 300,
    
    /// <summary>Script aux function 2</summary>
    ScriptAux2 = 301
}

/// <summary>
/// RC channel reversed state
/// </summary>
public enum RcReversed
{
    /// <summary>Channel not reversed (normal)</summary>
    Normal = 0,
    
    /// <summary>Channel reversed</summary>
    Reversed = 1
}

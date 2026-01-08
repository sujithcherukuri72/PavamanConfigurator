namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Motor test throttle type for MAVLink DO_MOTOR_TEST command.
/// Maps to MOTOR_TEST_THROTTLE_TYPE enum.
/// </summary>
public enum MotorTestThrottleType
{
    /// <summary>Throttle as a percentage (0-100)</summary>
    ThrottlePercent = 0,
    
    /// <summary>Throttle as PWM value (typically 1000-2000)</summary>
    ThrottlePwm = 1,
    
    /// <summary>Throttle pass-through from pilot input</summary>
    ThrottlePilot = 2,
    
    /// <summary>Compass calibration motor test</summary>
    CompassCal = 3
}

/// <summary>
/// Motor test order for sequential testing.
/// Maps to MOTOR_TEST_ORDER enum.
/// </summary>
public enum MotorTestOrder
{
    /// <summary>Default motor order (board defined)</summary>
    Default = 0,
    
    /// <summary>Motor number as sequence (1-based)</summary>
    Sequence = 1
}

/// <summary>
/// ESC calibration mode - matches ArduPilot ESC_CALIBRATION parameter
/// </summary>
public enum EscCalibrationMode
{
    /// <summary>ESC calibration disabled</summary>
    Disabled = 0,
    
    /// <summary>Power cycle to start calibration</summary>
    PowerCycleToStart = 1,
    
    /// <summary>Automatic calibration (unused)</summary>
    Auto = 2,
    
    /// <summary>Calibrate on next boot - pass-through mode</summary>
    PassthroughOnNextBoot = 3
}

/// <summary>
/// Motor output protocol type
/// </summary>
public enum MotorOutputType
{
    /// <summary>Normal PWM output</summary>
    Normal = 0,
    
    /// <summary>OneShot125 protocol</summary>
    OneShot = 1,
    
    /// <summary>OneShot42 protocol</summary>
    OneShot42 = 2,
    
    /// <summary>DShot150 protocol</summary>
    DShot150 = 3,
    
    /// <summary>DShot300 protocol</summary>
    DShot300 = 4,
    
    /// <summary>DShot600 protocol</summary>
    DShot600 = 5,
    
    /// <summary>DShot1200 protocol</summary>
    DShot1200 = 6,
    
    /// <summary>PWM range from MOT_PWM_MIN to MOT_PWM_MAX</summary>
    PwmRange = 7,
    
    /// <summary>BLHeli32 passthrough</summary>
    BLHeli32 = 8
}

/// <summary>
/// Motor spin direction
/// </summary>
public enum MotorSpinDirection
{
    /// <summary>Normal spin direction (default)</summary>
    Normal = 0,
    
    /// <summary>Reverse spin direction</summary>
    Reverse = 1
}

/// <summary>
/// Motor test state
/// </summary>
public enum MotorTestState
{
    /// <summary>Motor test idle</summary>
    Idle,
    
    /// <summary>Motor test in progress</summary>
    Testing,
    
    /// <summary>Motor test completed successfully</summary>
    Completed,
    
    /// <summary>Motor test failed</summary>
    Failed
}

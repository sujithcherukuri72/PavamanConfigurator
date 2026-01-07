using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Motor and ESC configuration settings.
/// Maps to ArduPilot MOT_* and ESC_* parameters.
/// </summary>
public class MotorEscSettings
{
    /// <summary>
    /// Number of motors on the vehicle (typically 4, 6, or 8)
    /// </summary>
    public int MotorCount { get; set; } = 4;

    /// <summary>
    /// Motor PWM type (MOT_PWM_TYPE)
    /// </summary>
    public MotorOutputType PwmType { get; set; } = MotorOutputType.Normal;

    /// <summary>
    /// Minimum PWM value for motors (MOT_PWM_MIN)
    /// </summary>
    public int PwmMin { get; set; } = 1000;

    /// <summary>
    /// Maximum PWM value for motors (MOT_PWM_MAX)
    /// </summary>
    public int PwmMax { get; set; } = 2000;

    /// <summary>
    /// Spin armed throttle (MOT_SPIN_ARM)
    /// Motor output when armed but not flying (0.0 to 0.3)
    /// </summary>
    public float SpinArmed { get; set; } = 0.1f;

    /// <summary>
    /// Spin minimum throttle (MOT_SPIN_MIN)
    /// Minimum throttle output to motors while flying (0.0 to 0.3)
    /// </summary>
    public float SpinMin { get; set; } = 0.15f;

    /// <summary>
    /// Spin maximum throttle (MOT_SPIN_MAX)
    /// Maximum throttle output to motors (0.9 to 1.0)
    /// </summary>
    public float SpinMax { get; set; } = 0.95f;

    /// <summary>
    /// Motor thrust hover (MOT_THST_HOVER)
    /// Estimated throttle for hover (0.1 to 0.8)
    /// </summary>
    public float ThrustHover { get; set; } = 0.35f;

    /// <summary>
    /// Motor thrust expo (MOT_THST_EXPO)
    /// Motor thrust curve expo (0.0 to 1.0)
    /// </summary>
    public float ThrustExpo { get; set; } = 0.65f;

    /// <summary>
    /// Battery voltage compensation (MOT_BAT_VOLT_MAX)
    /// </summary>
    public float BattVoltMax { get; set; } = 0.0f;

    /// <summary>
    /// Battery voltage minimum (MOT_BAT_VOLT_MIN)
    /// </summary>
    public float BattVoltMin { get; set; } = 0.0f;

    /// <summary>
    /// Motor slew rate (MOT_SLEWRATE)
    /// Slew rate limit on motor output (0 to 100)
    /// </summary>
    public float SlewRate { get; set; } = 0.0f;

    /// <summary>
    /// ESC calibration mode (ESC_CALIBRATION)
    /// </summary>
    public EscCalibrationMode EscCalibration { get; set; } = EscCalibrationMode.Disabled;

    /// <summary>
    /// Whether motor interlock is enabled
    /// </summary>
    public bool MotorInterlockEnabled { get; set; } = false;

    /// <summary>
    /// Whether BLHeli passthrough is available
    /// </summary>
    public bool BLHeliPassthroughAvailable { get; set; } = false;
}

/// <summary>
/// Motor test request parameters
/// </summary>
public class MotorTestRequest
{
    /// <summary>
    /// Motor number (1-based: 1, 2, 3, ...)
    /// </summary>
    public int MotorNumber { get; set; }

    /// <summary>
    /// Throttle type to use
    /// </summary>
    public MotorTestThrottleType ThrottleType { get; set; } = MotorTestThrottleType.ThrottlePercent;

    /// <summary>
    /// Throttle value (percentage 0-100 or PWM 1000-2000)
    /// </summary>
    public float ThrottleValue { get; set; }

    /// <summary>
    /// Test duration in seconds
    /// </summary>
    public float DurationSeconds { get; set; } = 1.0f;

    /// <summary>
    /// Motor test order
    /// </summary>
    public MotorTestOrder TestOrder { get; set; } = MotorTestOrder.Default;

    /// <summary>
    /// Number of test instances (0 = single test)
    /// </summary>
    public int TestCount { get; set; } = 1;
}

/// <summary>
/// Motor status information
/// </summary>
public class MotorStatus
{
    /// <summary>
    /// Motor number (1-based)
    /// </summary>
    public int MotorNumber { get; set; }

    /// <summary>
    /// Current throttle output percentage
    /// </summary>
    public float ThrottlePercent { get; set; }

    /// <summary>
    /// Current PWM output value
    /// </summary>
    public int PwmValue { get; set; }

    /// <summary>
    /// Motor RPM (if telemetry available)
    /// </summary>
    public float? Rpm { get; set; }

    /// <summary>
    /// Motor current draw in amps (if telemetry available)
    /// </summary>
    public float? CurrentAmps { get; set; }

    /// <summary>
    /// Motor temperature in Celsius (if telemetry available)
    /// </summary>
    public float? TemperatureCelsius { get; set; }

    /// <summary>
    /// Whether this motor is currently being tested
    /// </summary>
    public bool IsTesting { get; set; }

    /// <summary>
    /// Test state for this motor
    /// </summary>
    public MotorTestState TestState { get; set; } = MotorTestState.Idle;
}

/// <summary>
/// ESC telemetry data (for ESCs that support telemetry)
/// </summary>
public class EscTelemetry
{
    /// <summary>
    /// ESC/Motor number (1-based)
    /// </summary>
    public int EscNumber { get; set; }

    /// <summary>
    /// ESC voltage in volts
    /// </summary>
    public float Voltage { get; set; }

    /// <summary>
    /// ESC current in amps
    /// </summary>
    public float Current { get; set; }

    /// <summary>
    /// ESC temperature in Celsius
    /// </summary>
    public float Temperature { get; set; }

    /// <summary>
    /// Motor RPM
    /// </summary>
    public int Rpm { get; set; }

    /// <summary>
    /// ESC consumption in mAh
    /// </summary>
    public float ConsumptionMah { get; set; }
}

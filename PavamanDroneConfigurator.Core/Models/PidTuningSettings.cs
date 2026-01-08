using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Basic tuning parameters that affect overall flight feel.
/// These are high-level parameters that adjust multiple underlying PID values.
/// </summary>
public class BasicTuningSettings
{
    /// <summary>
    /// RC Feel Roll/Pitch - Controls responsiveness (ATC_INPUT_TC)
    /// Lower values = softer response, Higher values = crisper response
    /// Range: 0.0 to 1.0, Default: 0.15
    /// </summary>
    public float RcFeelRollPitch { get; set; } = 0.15f;

    /// <summary>
    /// Roll/Pitch Sensitivity - Rate controller sensitivity (ATC_ACCEL_R_MAX/ATC_ACCEL_P_MAX)
    /// Lower values = twitchy, Higher values = sluggish
    /// Range: 0.01 to 0.5
    /// </summary>
    public float RollPitchSensitivity { get; set; } = 0.135f;

    /// <summary>
    /// Climb Sensitivity - Throttle responsiveness (PILOT_ACCEL_Z)
    /// Lower values = gentle climb, Higher values = aggressive climb
    /// Range: 0.3 to 1.0, Default: 1.0 (250 cm/s/s)
    /// </summary>
    public float ClimbSensitivity { get; set; } = 1.0f;

    /// <summary>
    /// Spin While Armed - Motor idle speed when armed (MOT_SPIN_ARM)
    /// Range: 0.0 to 1.0, Default: 0.1
    /// </summary>
    public float SpinWhileArmed { get; set; } = 0.1f;

    /// <summary>
    /// Minimum Throttle - Minimum motor output (MOT_SPIN_MIN)
    /// Range: 0.0 to 0.3, Default: 0.15
    /// </summary>
    public float MinThrottle { get; set; } = 0.15f;
}

/// <summary>
/// Advanced axis-specific PID tuning parameters.
/// Direct access to ArduPilot ATC_ parameters.
/// </summary>
public class AxisPidSettings
{
    /// <summary>
    /// The axis these settings apply to
    /// </summary>
    public TuningAxis Axis { get; set; }

    /// <summary>
    /// Angle Controller P Gain (ATC_ANG_xxx_P)
    /// Controls how aggressively the copter tries to maintain the desired angle
    /// Range: 3.0 to 12.0, Default: 4.5
    /// </summary>
    public float AngleP { get; set; } = 4.5f;

    /// <summary>
    /// Rate Controller P Gain (ATC_RAT_xxx_P)
    /// Proportional gain for rate control
    /// Range: 0.01 to 0.5, Default: 0.135
    /// </summary>
    public float RateP { get; set; } = 0.135f;

    /// <summary>
    /// Rate Controller I Gain (ATC_RAT_xxx_I)
    /// Integral gain for rate control - corrects steady-state errors
    /// Range: 0.01 to 2.0, Default: 0.135
    /// </summary>
    public float RateI { get; set; } = 0.135f;

    /// <summary>
    /// Rate Controller D Gain (ATC_RAT_xxx_D)
    /// Derivative gain for rate control - dampens oscillations
    /// Range: 0.0 to 0.5, Default: 0.0036
    /// </summary>
    public float RateD { get; set; } = 0.0036f;

    /// <summary>
    /// Rate Controller Feed Forward (ATC_RAT_xxx_FF)
    /// Improves response to rapid stick movements
    /// Range: 0.0 to 0.5, Default: 0.0
    /// </summary>
    public float RateFF { get; set; } = 0.0f;

    /// <summary>
    /// Rate Controller Filter (ATC_RAT_xxx_FLTD/FLTT)
    /// Low-pass filter cutoff frequency for D-term
    /// Range: 0 to 256 Hz, Default: 20
    /// </summary>
    public float RateFilter { get; set; } = 20.0f;

    /// <summary>
    /// Rate Controller IMAX (ATC_RAT_xxx_IMAX)
    /// Maximum integrator output
    /// Range: 0 to 1, Default: 0.5
    /// </summary>
    public float RateIMax { get; set; } = 0.5f;
}

/// <summary>
/// AutoTune configuration settings.
/// Controls the automated PID tuning process.
/// </summary>
public class AutoTuneSettings
{
    /// <summary>
    /// Axes to include in AutoTune (AUTOTUNE_AXES)
    /// Bitmask: 1=Roll, 2=Pitch, 4=Yaw
    /// </summary>
    public AutoTuneAxes AxesToTune { get; set; } = AutoTuneAxes.Roll | AutoTuneAxes.Pitch | AutoTuneAxes.Yaw;

    /// <summary>
    /// RC channel assigned to AutoTune activation
    /// Set via RCx_OPTION = 17 (AutoTune)
    /// </summary>
    public AutoTuneChannel AutoTuneSwitch { get; set; } = AutoTuneChannel.None;

    /// <summary>
    /// AutoTune aggressiveness (AUTOTUNE_AGGR)
    /// Range: 0.05 to 0.1, Default: 0.1
    /// </summary>
    public float Aggressiveness { get; set; } = 0.1f;

    /// <summary>
    /// Minimum D gain during tuning (AUTOTUNE_MIN_D)
    /// Range: 0.001 to 0.006, Default: 0.001
    /// </summary>
    public float MinD { get; set; } = 0.001f;
}

/// <summary>
/// In-flight tuning via RC transmitter knob.
/// Allows real-time parameter adjustment during flight.
/// </summary>
public class InFlightTuningSettings
{
    /// <summary>
    /// RC Channel 6 tuning option (TUNE)
    /// Selects which parameter to tune via RC Channel 6
    /// </summary>
    public InFlightTuningOption TuneOption { get; set; } = InFlightTuningOption.None;

    /// <summary>
    /// Minimum value for tuning range (TUNE_MIN)
    /// </summary>
    public float TuneMin { get; set; } = 0.0f;

    /// <summary>
    /// Maximum value for tuning range (TUNE_MAX)
    /// </summary>
    public float TuneMax { get; set; } = 0.0f;
}

/// <summary>
/// Complete PID tuning configuration combining all settings.
/// </summary>
public class PidTuningConfiguration
{
    /// <summary>
    /// Basic tuning settings for overall flight feel
    /// </summary>
    public BasicTuningSettings BasicTuning { get; set; } = new();

    /// <summary>
    /// Roll axis PID settings
    /// </summary>
    public AxisPidSettings RollPid { get; set; } = new() { Axis = TuningAxis.Roll };

    /// <summary>
    /// Pitch axis PID settings
    /// </summary>
    public AxisPidSettings PitchPid { get; set; } = new() { Axis = TuningAxis.Pitch };

    /// <summary>
    /// Yaw axis PID settings
    /// </summary>
    public AxisPidSettings YawPid { get; set; } = new() { Axis = TuningAxis.Yaw };

    /// <summary>
    /// AutoTune configuration
    /// </summary>
    public AutoTuneSettings AutoTune { get; set; } = new();

    /// <summary>
    /// In-flight tuning settings
    /// </summary>
    public InFlightTuningSettings InFlightTuning { get; set; } = new();
}

/// <summary>
/// Parameter definition for display purposes
/// </summary>
public class PidParameterInfo
{
    public string ParameterName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float MinValue { get; set; }
    public float MaxValue { get; set; }
    public float DefaultValue { get; set; }
    public float Increment { get; set; } = 0.001f;
    public string Unit { get; set; } = string.Empty;
}

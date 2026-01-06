using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Comprehensive safety settings model for PDRL compliance.
/// Contains all safety-related parameters for drone configuration.
/// </summary>
public class SafetySettings
{
    #region Arming Checks
    
    /// <summary>Arming check bitmask (ARMING_CHECK)</summary>
    public ArmingCheck ArmingCheck { get; set; } = ArmingCheck.PDRLMinimum;
    
    /// <summary>Require GPS lock before arming</summary>
    public bool RequireGPSLock { get; set; } = true;
    
    #endregion

    #region Battery Failsafe
    
    /// <summary>Battery monitor type (BATT_MONITOR)</summary>
    public int BattMonitor { get; set; } = 4;
    
    /// <summary>Low battery voltage threshold in volts (BATT_LOW_VOLT)</summary>
    public float BattLowVolt { get; set; } = 10.5f;
    
    /// <summary>Critical battery voltage threshold in volts (BATT_CRT_VOLT)</summary>
    public float BattCritVolt { get; set; } = 10.0f;
    
    /// <summary>Low battery capacity threshold in mAh (BATT_LOW_MAH)</summary>
    public float BattLowMah { get; set; } = 0f;
    
    /// <summary>Critical battery capacity threshold in mAh (BATT_CRT_MAH)</summary>
    public float BattCritMah { get; set; } = 0f;
    
    /// <summary>Battery capacity in mAh (BATT_CAPACITY)</summary>
    public float BattCapacity { get; set; } = 3300f;
    
    /// <summary>Action on low battery (BATT_FS_LOW_ACT)</summary>
    public BatteryFailsafeAction BattFsLowAction { get; set; } = BatteryFailsafeAction.RTL;
    
    /// <summary>Action on critical battery (BATT_FS_CRT_ACT)</summary>
    public BatteryFailsafeAction BattFsCritAction { get; set; } = BatteryFailsafeAction.Land;
    
    /// <summary>Battery failsafe timer in seconds (BATT_FS_LOW_TIMER)</summary>
    public float BattFsLowTimer { get; set; } = 10f;
    
    #endregion

    #region RC/Throttle Failsafe
    
    /// <summary>Throttle failsafe action (FS_THR_ENABLE)</summary>
    public RCFailsafeAction RcFailsafeAction { get; set; } = RCFailsafeAction.AlwaysRTL;
    
    /// <summary>Throttle failsafe PWM value (FS_THR_VALUE)</summary>
    public float RcFailsafePwmValue { get; set; } = 975f;
    
    /// <summary>RC failsafe timeout in seconds (RC_FS_TIMEOUT)</summary>
    public float RcFailsafeTimeout { get; set; } = 1.0f;
    
    #endregion

    #region GCS Failsafe
    
    /// <summary>GCS failsafe action (FS_GCS_ENABLE)</summary>
    public GCSFailsafeAction GcsFailsafeAction { get; set; } = GCSFailsafeAction.Disabled;
    
    /// <summary>GCS failsafe timeout in seconds (FS_GCS_TIMEOUT)</summary>
    public float GcsFailsafeTimeout { get; set; } = 5.0f;
    
    #endregion

    #region Geofence
    
    /// <summary>Fence enabled (FENCE_ENABLE)</summary>
    public bool FenceEnabled { get; set; }
    
    /// <summary>Fence type bitmask (FENCE_TYPE)</summary>
    public FenceType FenceType { get; set; } = FenceType.AltitudeMax | FenceType.Circle;
    
    /// <summary>Fence breach action (FENCE_ACTION)</summary>
    public FenceAction FenceAction { get; set; } = FenceAction.RTLOrLand;
    
    /// <summary>Maximum altitude in meters (FENCE_ALT_MAX)</summary>
    public float FenceAltMax { get; set; } = 100f;
    
    /// <summary>Minimum altitude in meters (FENCE_ALT_MIN)</summary>
    public float FenceAltMin { get; set; } = -10f;
    
    /// <summary>Circular fence radius in meters (FENCE_RADIUS)</summary>
    public float FenceRadius { get; set; } = 300f;
    
    /// <summary>Fence margin in meters (FENCE_MARGIN)</summary>
    public float FenceMargin { get; set; } = 2f;
    
    /// <summary>Enable floor fence (FENCE_FLOOR_ENABLE)</summary>
    public bool FenceFloorEnabled { get; set; }
    
    #endregion

    #region EKF Failsafe
    
    /// <summary>EKF failsafe action (FS_EKF_ACTION)</summary>
    public EKFFailsafeAction EkfFailsafeAction { get; set; } = EKFFailsafeAction.Land;
    
    /// <summary>EKF failsafe variance threshold (FS_EKF_THRESH)</summary>
    public float EkfFailsafeThreshold { get; set; } = 0.8f;
    
    #endregion

    #region Vibration Failsafe
    
    /// <summary>Vibration failsafe action (FS_VIBE_ENABLE)</summary>
    public VibrationFailsafeAction VibrationFailsafeAction { get; set; } = VibrationFailsafeAction.WarnOnly;
    
    #endregion

    #region Crash Detection
    
    /// <summary>Crash check action (FS_CRASH_CHECK)</summary>
    public CrashCheckAction CrashCheckAction { get; set; } = CrashCheckAction.Disarm;
    
    #endregion

    #region Motor Safety
    
    /// <summary>Motor safe disarm behavior (MOT_SAFE_DISARM)</summary>
    public MotorSafetyDisarm MotorSafeDisarm { get; set; } = MotorSafetyDisarm.DisarmWhenLanded;
    
    /// <summary>Motor spin when armed but not flying (MOT_SPIN_ARM)</summary>
    public float MotorSpinArm { get; set; } = 0.1f;
    
    /// <summary>Motor spin minimum (MOT_SPIN_MIN)</summary>
    public float MotorSpinMin { get; set; } = 0.15f;
    
    /// <summary>Disarm delay in seconds (DISARM_DELAY)</summary>
    public float DisarmDelay { get; set; } = 10f;
    
    #endregion

    #region RTL (Return To Launch) Settings
    
    /// <summary>RTL altitude in centimeters (RTL_ALT)</summary>
    public float RtlAltitude { get; set; } = 1500f;
    
    /// <summary>RTL final altitude in centimeters (RTL_ALT_FINAL)</summary>
    public float RtlFinalAltitude { get; set; } = 0f;
    
    /// <summary>RTL climb minimum altitude (RTL_CLIMB_MIN)</summary>
    public float RtlClimbMin { get; set; } = 0f;
    
    /// <summary>RTL loiter time before landing in milliseconds (RTL_LOIT_TIME)</summary>
    public float RtlLoiterTime { get; set; } = 5000f;
    
    /// <summary>RTL speed in cm/s (RTL_SPEED)</summary>
    public float RtlSpeed { get; set; } = 0f;
    
    /// <summary>RTL cone slope (RTL_CONE_SLOPE)</summary>
    public float RtlConeSlope { get; set; } = 3f;
    
    #endregion

    #region Land Settings
    
    /// <summary>Land speed in cm/s (LAND_SPEED)</summary>
    public float LandSpeed { get; set; } = 50f;
    
    /// <summary>Land speed high in cm/s (LAND_SPEED_HIGH)</summary>
    public float LandSpeedHigh { get; set; } = 0f;
    
    /// <summary>Land altitude threshold in cm (LAND_ALT_LOW)</summary>
    public float LandAltLow { get; set; } = 1000f;
    
    /// <summary>Require reposition before landing</summary>
    public bool LandRepositionEnabled { get; set; } = true;
    
    #endregion

    #region Parachute Settings
    
    /// <summary>Parachute enabled (CHUTE_ENABLED)</summary>
    public ParachuteEnabled ParachuteEnabled { get; set; } = ParachuteEnabled.Disabled;
    
    /// <summary>Parachute release altitude in meters (CHUTE_ALT_MIN)</summary>
    public float ParachuteAltMin { get; set; } = 10f;
    
    #endregion

    #region PDRL Specific Settings
    
    /// <summary>Maximum allowed flight time in minutes (PDRL compliance)</summary>
    public float MaxFlightTime { get; set; } = 30f;
    
    /// <summary>Pre-flight check required before arming</summary>
    public bool PreflightCheckRequired { get; set; } = true;
    
    /// <summary>Pilot in command acknowledgment required</summary>
    public bool PilotAcknowledgmentRequired { get; set; } = true;
    
    #endregion
}

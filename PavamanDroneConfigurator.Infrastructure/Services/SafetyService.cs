using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for managing drone safety settings with PDRL compliance validation.
/// </summary>
public class SafetyService : ISafetyService
{
    private readonly IParameterService _parameterService;
    private readonly ILogger<SafetyService> _logger;

    public event EventHandler<SafetySettings>? SafetySettingsChanged;
    public event EventHandler<string>? SafetyWarning;

    public SafetyService(
        IParameterService parameterService,
        ILogger<SafetyService> logger)
    {
        _parameterService = parameterService;
        _logger = logger;
    }

    public async Task<SafetySettings?> GetSafetySettingsAsync()
    {
        try
        {
            _logger.LogInformation("Getting safety settings");

            var settings = new SafetySettings();

            // Arming Checks
            settings.ArmingCheck = (ArmingCheck)(int)await GetParameterValueAsync("ARMING_CHECK");

            // Battery Failsafe
            settings.BattMonitor = (int)await GetParameterValueAsync("BATT_MONITOR");
            settings.BattLowVolt = await GetParameterValueAsync("BATT_LOW_VOLT");
            settings.BattCritVolt = await GetParameterValueAsync("BATT_CRT_VOLT");
            settings.BattLowMah = await GetParameterValueAsync("BATT_LOW_MAH");
            settings.BattCritMah = await GetParameterValueAsync("BATT_CRT_MAH");
            settings.BattCapacity = await GetParameterValueAsync("BATT_CAPACITY");
            settings.BattFsLowAction = (BatteryFailsafeAction)(int)await GetParameterValueAsync("BATT_FS_LOW_ACT");
            settings.BattFsCritAction = (BatteryFailsafeAction)(int)await GetParameterValueAsync("BATT_FS_CRT_ACT");

            // RC Failsafe
            settings.RcFailsafeAction = (RCFailsafeAction)(int)await GetParameterValueAsync("FS_THR_ENABLE");
            settings.RcFailsafePwmValue = await GetParameterValueAsync("FS_THR_VALUE");

            // GCS Failsafe
            settings.GcsFailsafeAction = (GCSFailsafeAction)(int)await GetParameterValueAsync("FS_GCS_ENABLE");
            settings.GcsFailsafeTimeout = await GetParameterValueAsync("FS_GCS_TIMEOUT");

            // Geofence
            settings.FenceEnabled = await GetParameterValueAsync("FENCE_ENABLE") > 0;
            settings.FenceType = (FenceType)(int)await GetParameterValueAsync("FENCE_TYPE");
            settings.FenceAction = (FenceAction)(int)await GetParameterValueAsync("FENCE_ACTION");
            settings.FenceAltMax = await GetParameterValueAsync("FENCE_ALT_MAX");
            settings.FenceAltMin = await GetParameterValueAsync("FENCE_ALT_MIN");
            settings.FenceRadius = await GetParameterValueAsync("FENCE_RADIUS");
            settings.FenceMargin = await GetParameterValueAsync("FENCE_MARGIN");

            // EKF Failsafe
            settings.EkfFailsafeAction = (EKFFailsafeAction)(int)await GetParameterValueAsync("FS_EKF_ACTION");
            settings.EkfFailsafeThreshold = await GetParameterValueAsync("FS_EKF_THRESH");

            // Vibration Failsafe
            settings.VibrationFailsafeAction = (VibrationFailsafeAction)(int)await GetParameterValueAsync("FS_VIBE_ENABLE");

            // Crash Check
            settings.CrashCheckAction = (CrashCheckAction)(int)await GetParameterValueAsync("FS_CRASH_CHECK");

            // Motor Safety
            settings.MotorSafeDisarm = (MotorSafetyDisarm)(int)await GetParameterValueAsync("MOT_SAFE_DISARM");
            settings.DisarmDelay = await GetParameterValueAsync("DISARM_DELAY");

            // RTL Settings
            settings.RtlAltitude = await GetParameterValueAsync("RTL_ALT");
            settings.RtlFinalAltitude = await GetParameterValueAsync("RTL_ALT_FINAL");
            settings.RtlLoiterTime = await GetParameterValueAsync("RTL_LOIT_TIME");
            settings.RtlSpeed = await GetParameterValueAsync("RTL_SPEED");

            // Land Settings
            settings.LandSpeed = await GetParameterValueAsync("LAND_SPEED");
            settings.LandSpeedHigh = await GetParameterValueAsync("LAND_SPEED_HIGH");

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting safety settings");
            return null;
        }
    }

    public async Task<bool> UpdateSafetySettingsAsync(SafetySettings settings)
    {
        try
        {
            _logger.LogInformation("Updating safety settings");
            
            var failedParameters = new List<string>();
            var successCount = 0;
            var totalParameters = 0;

            async Task<bool> SetAndTrack(string name, float value)
            {
                totalParameters++;
                try
                {
                    var result = await _parameterService.SetParameterAsync(name, value);
                    if (result)
                    {
                        successCount++;
                        _logger.LogDebug("Set {Name} = {Value}", name, value);
                    }
                    else
                    {
                        failedParameters.Add(name);
                        _logger.LogWarning("Failed to set {Name} = {Value}", name, value);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    failedParameters.Add($"{name} ({ex.Message})");
                    _logger.LogError(ex, "Exception setting {Name}", name);
                    return false;
                }
            }

            // Arming Checks
            await SetAndTrack("ARMING_CHECK", (float)(int)settings.ArmingCheck);

            // Battery Failsafe
            await SetAndTrack("BATT_MONITOR", settings.BattMonitor);
            await SetAndTrack("BATT_LOW_VOLT", settings.BattLowVolt);
            await SetAndTrack("BATT_CRT_VOLT", settings.BattCritVolt);
            await SetAndTrack("BATT_CAPACITY", settings.BattCapacity);
            await SetAndTrack("BATT_FS_LOW_ACT", (float)(int)settings.BattFsLowAction);
            await SetAndTrack("BATT_FS_CRT_ACT", (float)(int)settings.BattFsCritAction);

            // RC Failsafe
            await SetAndTrack("FS_THR_ENABLE", (float)(int)settings.RcFailsafeAction);
            await SetAndTrack("FS_THR_VALUE", settings.RcFailsafePwmValue);

            // GCS Failsafe
            await SetAndTrack("FS_GCS_ENABLE", (float)(int)settings.GcsFailsafeAction);
            await SetAndTrack("FS_GCS_TIMEOUT", settings.GcsFailsafeTimeout);

            // Geofence
            await SetAndTrack("FENCE_ENABLE", settings.FenceEnabled ? 1f : 0f);
            await SetAndTrack("FENCE_TYPE", (float)(int)settings.FenceType);
            await SetAndTrack("FENCE_ACTION", (float)(int)settings.FenceAction);
            await SetAndTrack("FENCE_ALT_MAX", settings.FenceAltMax);
            await SetAndTrack("FENCE_RADIUS", settings.FenceRadius);

            // EKF Failsafe
            await SetAndTrack("FS_EKF_ACTION", (float)(int)settings.EkfFailsafeAction);

            // Crash Check
            await SetAndTrack("FS_CRASH_CHECK", (float)(int)settings.CrashCheckAction);

            // Motor Safety
            await SetAndTrack("DISARM_DELAY", settings.DisarmDelay);

            // RTL Settings
            await SetAndTrack("RTL_ALT", settings.RtlAltitude);
            await SetAndTrack("RTL_ALT_FINAL", settings.RtlFinalAltitude);
            await SetAndTrack("RTL_LOIT_TIME", settings.RtlLoiterTime);
            await SetAndTrack("RTL_SPEED", settings.RtlSpeed);

            // Land Settings
            await SetAndTrack("LAND_SPEED", settings.LandSpeed);

            _logger.LogInformation("Parameter update: {Success}/{Total} succeeded", successCount, totalParameters);
            
            if (failedParameters.Count > 0)
            {
                _logger.LogWarning("Failed parameters: {FailedParams}", string.Join(", ", failedParameters));
            }

            // Notify listeners
            SafetySettingsChanged?.Invoke(this, settings);
            
            return successCount >= (totalParameters / 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating safety settings");
            return false;
        }
    }

    public Task<List<string>> ValidatePDRLComplianceAsync(SafetySettings settings)
    {
        var warnings = new List<string>();

        // Check arming requirements
        if (!settings.ArmingCheck.HasFlag(ArmingCheck.GPS))
            warnings.Add("GPS arming check disabled - PDRL requires GPS lock before flight");
        
        if (!settings.ArmingCheck.HasFlag(ArmingCheck.Battery))
            warnings.Add("Battery arming check disabled - PDRL requires battery monitoring");

        if (!settings.ArmingCheck.HasFlag(ArmingCheck.Compass))
            warnings.Add("Compass check disabled - recommended for PDRL compliance");

        // Check battery failsafe
        if (settings.BattFsLowAction == BatteryFailsafeAction.Disabled)
        {
            warnings.Add("Low battery failsafe disabled - PDRL requires failsafe action");
            SafetyWarning?.Invoke(this, "Low battery failsafe is disabled!");
        }

        if (settings.BattFsCritAction == BatteryFailsafeAction.Disabled)
        {
            warnings.Add("Critical battery failsafe disabled - dangerous configuration!");
            SafetyWarning?.Invoke(this, "Critical battery failsafe is disabled!");
        }

        // Check RC failsafe
        if (settings.RcFailsafeAction == RCFailsafeAction.Disabled)
        {
            warnings.Add("RC failsafe disabled - PDRL requires RC loss protection");
            SafetyWarning?.Invoke(this, "RC failsafe is disabled!");
        }

        // Check geofence
        if (!settings.FenceEnabled)
        {
            warnings.Add("Geofence disabled - PDRL recommends altitude and radius limits");
        }
        else
        {
            if (settings.FenceAltMax > 120)
                warnings.Add($"Fence max altitude ({settings.FenceAltMax}m) exceeds PDRL limit of 120m AGL");
        }

        // Check RTL altitude
        if (settings.RtlAltitude / 100f > 120) // RTL_ALT is in cm
        {
            warnings.Add($"RTL altitude ({settings.RtlAltitude / 100f}m) exceeds PDRL limit of 120m");
        }

        foreach (var warning in warnings)
        {
            _logger.LogWarning("PDRL Compliance: {Warning}", warning);
        }

        return Task.FromResult(warnings);
    }

    public Task<SafetySettings> GetPDRLDefaultsAsync()
    {
        var defaults = new SafetySettings
        {
            // Arming - enable all critical checks
            ArmingCheck = ArmingCheck.All | ArmingCheck.Barometer | ArmingCheck.Compass | 
                         ArmingCheck.GPS | ArmingCheck.INS | ArmingCheck.RC | ArmingCheck.Battery,
            RequireGPSLock = true,

            // Battery failsafe - RTL on low, Land on critical
            BattMonitor = 4,
            BattLowVolt = 10.5f,
            BattCritVolt = 10.0f,
            BattCapacity = 3300f,
            BattFsLowAction = BatteryFailsafeAction.RTL,
            BattFsCritAction = BatteryFailsafeAction.Land,
            BattFsLowTimer = 10f,

            // RC failsafe - Always RTL
            RcFailsafeAction = RCFailsafeAction.AlwaysRTL,
            RcFailsafePwmValue = 975f,

            // GCS failsafe - RTL after 5 seconds
            GcsFailsafeAction = GCSFailsafeAction.RTL,
            GcsFailsafeTimeout = 5.0f,

            // Geofence - 120m max altitude, 300m radius (PDRL compliant)
            FenceEnabled = true,
            FenceType = FenceType.AltitudeMax | FenceType.Circle,
            FenceAction = FenceAction.RTLOrLand,
            FenceAltMax = 120f,
            FenceAltMin = -10f,
            FenceRadius = 300f,
            FenceMargin = 2f,

            // EKF failsafe - Land on failure
            EkfFailsafeAction = EKFFailsafeAction.Land,
            EkfFailsafeThreshold = 0.8f,

            // Vibration failsafe - Warn only
            VibrationFailsafeAction = VibrationFailsafeAction.WarnOnly,

            // Crash check - Disarm
            CrashCheckAction = CrashCheckAction.Disarm,

            // Motor safety
            MotorSafeDisarm = MotorSafetyDisarm.DisarmWhenLanded,
            DisarmDelay = 10f,

            // RTL - 15m altitude
            RtlAltitude = 1500f, // cm
            RtlFinalAltitude = 0f,
            RtlLoiterTime = 5000f, // ms
            RtlSpeed = 0f,

            // Land
            LandSpeed = 50f,
            LandSpeedHigh = 0f,

            // PDRL specific
            MaxFlightTime = 30f,
            PreflightCheckRequired = true,
            PilotAcknowledgmentRequired = true
        };

        return Task.FromResult(defaults);
    }

    public async Task<ArmingCheck> GetArmingChecksAsync()
    {
        var value = await GetParameterValueAsync("ARMING_CHECK");
        return (ArmingCheck)(int)value;
    }

    public async Task<bool> SetArmingChecksAsync(ArmingCheck checks)
    {
        var result = await _parameterService.SetParameterAsync("ARMING_CHECK", (float)(int)checks);
        if (result)
        {
            _logger.LogInformation("Arming checks set to: {Checks}", checks);
        }
        return result;
    }

    public async Task<(bool Enabled, FenceType Type, FenceAction Action, float AltMax, float Radius)> GetGeofenceSettingsAsync()
    {
        var enabled = await GetParameterValueAsync("FENCE_ENABLE") > 0;
        var type = (FenceType)(int)await GetParameterValueAsync("FENCE_TYPE");
        var action = (FenceAction)(int)await GetParameterValueAsync("FENCE_ACTION");
        var altMax = await GetParameterValueAsync("FENCE_ALT_MAX");
        var radius = await GetParameterValueAsync("FENCE_RADIUS");

        return (enabled, type, action, altMax, radius);
    }

    public async Task<bool> SetGeofenceSettingsAsync(bool enabled, FenceType type, FenceAction action, float altMax, float radius)
    {
        var success = true;
        success &= await _parameterService.SetParameterAsync("FENCE_ENABLE", enabled ? 1f : 0f);
        success &= await _parameterService.SetParameterAsync("FENCE_TYPE", (float)(int)type);
        success &= await _parameterService.SetParameterAsync("FENCE_ACTION", (float)(int)action);
        success &= await _parameterService.SetParameterAsync("FENCE_ALT_MAX", altMax);
        success &= await _parameterService.SetParameterAsync("FENCE_RADIUS", radius);

        if (success)
        {
            _logger.LogInformation("Geofence settings updated: Enabled={Enabled}, AltMax={AltMax}m, Radius={Radius}m", 
                enabled, altMax, radius);
        }

        return success;
    }

    public async Task<(float Altitude, float FinalAltitude, float LoiterTime, float Speed)> GetRTLSettingsAsync()
    {
        var altitude = await GetParameterValueAsync("RTL_ALT");
        var finalAltitude = await GetParameterValueAsync("RTL_ALT_FINAL");
        var loiterTime = await GetParameterValueAsync("RTL_LOIT_TIME");
        var speed = await GetParameterValueAsync("RTL_SPEED");

        return (altitude, finalAltitude, loiterTime, speed);
    }

    public async Task<bool> SetRTLSettingsAsync(float altitude, float finalAltitude, float loiterTime, float speed)
    {
        var success = true;
        success &= await _parameterService.SetParameterAsync("RTL_ALT", altitude);
        success &= await _parameterService.SetParameterAsync("RTL_ALT_FINAL", finalAltitude);
        success &= await _parameterService.SetParameterAsync("RTL_LOIT_TIME", loiterTime);
        success &= await _parameterService.SetParameterAsync("RTL_SPEED", speed);

        if (success)
        {
            _logger.LogInformation("RTL settings updated: Alt={Alt}cm, FinalAlt={FinalAlt}cm, Loiter={Loiter}ms", 
                altitude, finalAltitude, loiterTime);
        }

        return success;
    }

    private async Task<float> GetParameterValueAsync(string name)
    {
        var param = await _parameterService.GetParameterAsync(name);
        return param?.Value ?? 0f;
    }
}

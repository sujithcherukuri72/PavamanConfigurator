using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service implementation for PID tuning operations.
/// Reads/writes ArduCopter PID parameters via MAVLink parameter protocol.
/// </summary>
public class PidTuningService : IPidTuningService
{
    private readonly ILogger<PidTuningService> _logger;
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    // ArduCopter parameter names for PID tuning
    private static class Parameters
    {
        // Basic Tuning
        public const string ATC_INPUT_TC = "ATC_INPUT_TC";           // RC feel
        public const string ATC_ACCEL_R_MAX = "ATC_ACCEL_R_MAX";     // Roll accel max
        public const string ATC_ACCEL_P_MAX = "ATC_ACCEL_P_MAX";     // Pitch accel max
        public const string ATC_ACCEL_Y_MAX = "ATC_ACCEL_Y_MAX";     // Yaw accel max
        public const string PILOT_ACCEL_Z = "PILOT_ACCEL_Z";         // Climb rate accel
        public const string MOT_SPIN_ARM = "MOT_SPIN_ARM";           // Motor spin when armed
        public const string MOT_SPIN_MIN = "MOT_SPIN_MIN";           // Motor minimum throttle

        // Roll Axis
        public const string ATC_ANG_RLL_P = "ATC_ANG_RLL_P";
        public const string ATC_RAT_RLL_P = "ATC_RAT_RLL_P";
        public const string ATC_RAT_RLL_I = "ATC_RAT_RLL_I";
        public const string ATC_RAT_RLL_D = "ATC_RAT_RLL_D";
        public const string ATC_RAT_RLL_FF = "ATC_RAT_RLL_FF";
        public const string ATC_RAT_RLL_FLTD = "ATC_RAT_RLL_FLTD";
        public const string ATC_RAT_RLL_FLTT = "ATC_RAT_RLL_FLTT";
        public const string ATC_RAT_RLL_IMAX = "ATC_RAT_RLL_IMAX";

        // Pitch Axis
        public const string ATC_ANG_PIT_P = "ATC_ANG_PIT_P";
        public const string ATC_RAT_PIT_P = "ATC_RAT_PIT_P";
        public const string ATC_RAT_PIT_I = "ATC_RAT_PIT_I";
        public const string ATC_RAT_PIT_D = "ATC_RAT_PIT_D";
        public const string ATC_RAT_PIT_FF = "ATC_RAT_PIT_FF";
        public const string ATC_RAT_PIT_FLTD = "ATC_RAT_PIT_FLTD";
        public const string ATC_RAT_PIT_FLTT = "ATC_RAT_PIT_FLTT";
        public const string ATC_RAT_PIT_IMAX = "ATC_RAT_PIT_IMAX";

        // Yaw Axis
        public const string ATC_ANG_YAW_P = "ATC_ANG_YAW_P";
        public const string ATC_RAT_YAW_P = "ATC_RAT_YAW_P";
        public const string ATC_RAT_YAW_I = "ATC_RAT_YAW_I";
        public const string ATC_RAT_YAW_D = "ATC_RAT_YAW_D";
        public const string ATC_RAT_YAW_FF = "ATC_RAT_YAW_FF";
        public const string ATC_RAT_YAW_FLTD = "ATC_RAT_YAW_FLTD";
        public const string ATC_RAT_YAW_FLTT = "ATC_RAT_YAW_FLTT";
        public const string ATC_RAT_YAW_IMAX = "ATC_RAT_YAW_IMAX";

        // AutoTune
        public const string AUTOTUNE_AXES = "AUTOTUNE_AXES";
        public const string AUTOTUNE_AGGR = "AUTOTUNE_AGGR";
        public const string AUTOTUNE_MIN_D = "AUTOTUNE_MIN_D";

        // In-flight tuning
        public const string TUNE = "TUNE";
        public const string TUNE_MIN = "TUNE_MIN";
        public const string TUNE_MAX = "TUNE_MAX";

        // RC options template (replace x with channel number)
        public const string RC_OPTION_TEMPLATE = "RC{0}_OPTION";
    }

    public event EventHandler<PidTuningConfiguration>? SettingsChanged;
    public event EventHandler<string>? ParameterUpdated;

    public PidTuningService(
        ILogger<PidTuningService> logger,
        IParameterService parameterService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _parameterService = parameterService;
        _connectionService = connectionService;

        _parameterService.ParameterUpdated += OnParameterUpdated;
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        // Check if this is a PID-related parameter
        if (IsPidParameter(parameterName))
        {
            ParameterUpdated?.Invoke(this, parameterName);
        }
    }

    private static bool IsPidParameter(string name)
    {
        return name.StartsWith("ATC_", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("MOT_SPIN", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("PILOT_ACCEL", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("AUTOTUNE", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("TUNE", StringComparison.OrdinalIgnoreCase);
    }

    #region Helper Methods

    private async Task<float?> GetParameterValueAsync(string name)
    {
        var param = await _parameterService.GetParameterAsync(name);
        return param?.Value;
    }

    private async Task<bool> SetParameterValueAsync(string name, float value)
    {
        var result = await _parameterService.SetParameterAsync(name, value);
        if (result)
        {
            _logger.LogInformation("Set {Parameter} = {Value}", name, value);
        }
        else
        {
            _logger.LogWarning("Failed to set {Parameter} = {Value}", name, value);
        }
        return result;
    }

    private string GetAxisPrefix(TuningAxis axis)
    {
        return axis switch
        {
            TuningAxis.Roll => "RLL",
            TuningAxis.Pitch => "PIT",
            TuningAxis.Yaw => "YAW",
            _ => "RLL"
        };
    }

    #endregion

    #region Basic Tuning

    public async Task<BasicTuningSettings?> GetBasicTuningSettingsAsync()
    {
        try
        {
            var settings = new BasicTuningSettings();

            var inputTc = await GetParameterValueAsync(Parameters.ATC_INPUT_TC);
            if (inputTc.HasValue) settings.RcFeelRollPitch = inputTc.Value;

            var accelRMax = await GetParameterValueAsync(Parameters.ATC_ACCEL_R_MAX);
            if (accelRMax.HasValue)
            {
                // Convert from deg/s/s to normalized sensitivity (inverse relationship)
                // Default is 110000 = 0.135 sensitivity
                settings.RollPitchSensitivity = accelRMax.Value / 110000f * 0.135f;
            }

            var pilotAccelZ = await GetParameterValueAsync(Parameters.PILOT_ACCEL_Z);
            if (pilotAccelZ.HasValue)
            {
                // Convert from cm/s/s to normalized (default 250 = 1.0)
                settings.ClimbSensitivity = pilotAccelZ.Value / 250f;
            }

            var spinArm = await GetParameterValueAsync(Parameters.MOT_SPIN_ARM);
            if (spinArm.HasValue) settings.SpinWhileArmed = spinArm.Value;

            var spinMin = await GetParameterValueAsync(Parameters.MOT_SPIN_MIN);
            if (spinMin.HasValue) settings.MinThrottle = spinMin.Value;

            _logger.LogInformation("Loaded basic tuning settings");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting basic tuning settings");
            return null;
        }
    }

    public async Task<bool> UpdateBasicTuningSettingsAsync(BasicTuningSettings settings)
    {
        try
        {
            var success = true;

            success &= await SetParameterValueAsync(Parameters.ATC_INPUT_TC, settings.RcFeelRollPitch);

            // Convert sensitivity to accel max values
            var accelMax = settings.RollPitchSensitivity / 0.135f * 110000f;
            success &= await SetParameterValueAsync(Parameters.ATC_ACCEL_R_MAX, accelMax);
            success &= await SetParameterValueAsync(Parameters.ATC_ACCEL_P_MAX, accelMax);

            // Convert climb sensitivity to cm/s/s
            var pilotAccelZ = settings.ClimbSensitivity * 250f;
            success &= await SetParameterValueAsync(Parameters.PILOT_ACCEL_Z, pilotAccelZ);

            success &= await SetParameterValueAsync(Parameters.MOT_SPIN_ARM, settings.SpinWhileArmed);
            success &= await SetParameterValueAsync(Parameters.MOT_SPIN_MIN, settings.MinThrottle);

            _logger.LogInformation("Updated basic tuning settings, success={Success}", success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating basic tuning settings");
            return false;
        }
    }

    public Task<bool> SetRcFeelAsync(float value)
    {
        return SetParameterValueAsync(Parameters.ATC_INPUT_TC, Math.Clamp(value, 0f, 1f));
    }

    public async Task<bool> SetRollPitchSensitivityAsync(float value)
    {
        var accelMax = value / 0.135f * 110000f;
        var success = await SetParameterValueAsync(Parameters.ATC_ACCEL_R_MAX, accelMax);
        success &= await SetParameterValueAsync(Parameters.ATC_ACCEL_P_MAX, accelMax);
        return success;
    }

    public Task<bool> SetClimbSensitivityAsync(float value)
    {
        var pilotAccelZ = Math.Clamp(value, 0.3f, 1f) * 250f;
        return SetParameterValueAsync(Parameters.PILOT_ACCEL_Z, pilotAccelZ);
    }

    public Task<bool> SetSpinWhileArmedAsync(float value)
    {
        return SetParameterValueAsync(Parameters.MOT_SPIN_ARM, Math.Clamp(value, 0f, 1f));
    }

    #endregion

    #region Advanced Tuning (Per-Axis PID)

    public async Task<AxisPidSettings?> GetAxisPidSettingsAsync(TuningAxis axis)
    {
        try
        {
            var prefix = GetAxisPrefix(axis);
            var settings = new AxisPidSettings { Axis = axis };

            var angleP = await GetParameterValueAsync($"ATC_ANG_{prefix}_P");
            if (angleP.HasValue) settings.AngleP = angleP.Value;

            var rateP = await GetParameterValueAsync($"ATC_RAT_{prefix}_P");
            if (rateP.HasValue) settings.RateP = rateP.Value;

            var rateI = await GetParameterValueAsync($"ATC_RAT_{prefix}_I");
            if (rateI.HasValue) settings.RateI = rateI.Value;

            var rateD = await GetParameterValueAsync($"ATC_RAT_{prefix}_D");
            if (rateD.HasValue) settings.RateD = rateD.Value;

            var rateFF = await GetParameterValueAsync($"ATC_RAT_{prefix}_FF");
            if (rateFF.HasValue) settings.RateFF = rateFF.Value;

            var rateFltd = await GetParameterValueAsync($"ATC_RAT_{prefix}_FLTD");
            if (rateFltd.HasValue) settings.RateFilter = rateFltd.Value;

            var rateIMax = await GetParameterValueAsync($"ATC_RAT_{prefix}_IMAX");
            if (rateIMax.HasValue) settings.RateIMax = rateIMax.Value;

            _logger.LogInformation("Loaded {Axis} axis PID settings", axis);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting {Axis} axis PID settings", axis);
            return null;
        }
    }

    public async Task<bool> UpdateAxisPidSettingsAsync(AxisPidSettings settings)
    {
        try
        {
            var prefix = GetAxisPrefix(settings.Axis);
            var success = true;

            success &= await SetParameterValueAsync($"ATC_ANG_{prefix}_P", settings.AngleP);
            success &= await SetParameterValueAsync($"ATC_RAT_{prefix}_P", settings.RateP);
            success &= await SetParameterValueAsync($"ATC_RAT_{prefix}_I", settings.RateI);
            success &= await SetParameterValueAsync($"ATC_RAT_{prefix}_D", settings.RateD);
            success &= await SetParameterValueAsync($"ATC_RAT_{prefix}_FF", settings.RateFF);
            success &= await SetParameterValueAsync($"ATC_RAT_{prefix}_FLTD", settings.RateFilter);
            success &= await SetParameterValueAsync($"ATC_RAT_{prefix}_IMAX", settings.RateIMax);

            _logger.LogInformation("Updated {Axis} axis PID settings, success={Success}", settings.Axis, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating {Axis} axis PID settings", settings.Axis);
            return false;
        }
    }

    public Task<bool> SetAnglePAsync(TuningAxis axis, float value)
    {
        var prefix = GetAxisPrefix(axis);
        return SetParameterValueAsync($"ATC_ANG_{prefix}_P", Math.Clamp(value, 3f, 12f));
    }

    public Task<bool> SetRatePAsync(TuningAxis axis, float value)
    {
        var prefix = GetAxisPrefix(axis);
        return SetParameterValueAsync($"ATC_RAT_{prefix}_P", Math.Clamp(value, 0.01f, 0.5f));
    }

    public Task<bool> SetRateIAsync(TuningAxis axis, float value)
    {
        var prefix = GetAxisPrefix(axis);
        return SetParameterValueAsync($"ATC_RAT_{prefix}_I", Math.Clamp(value, 0.01f, 2f));
    }

    public Task<bool> SetRateDAsync(TuningAxis axis, float value)
    {
        var prefix = GetAxisPrefix(axis);
        return SetParameterValueAsync($"ATC_RAT_{prefix}_D", Math.Clamp(value, 0f, 0.5f));
    }

    public Task<bool> SetRateFFAsync(TuningAxis axis, float value)
    {
        var prefix = GetAxisPrefix(axis);
        return SetParameterValueAsync($"ATC_RAT_{prefix}_FF", Math.Clamp(value, 0f, 0.5f));
    }

    public Task<bool> SetRateFilterAsync(TuningAxis axis, float value)
    {
        var prefix = GetAxisPrefix(axis);
        return SetParameterValueAsync($"ATC_RAT_{prefix}_FLTD", Math.Clamp(value, 0f, 256f));
    }

    #endregion

    #region AutoTune

    public async Task<AutoTuneSettings?> GetAutoTuneSettingsAsync()
    {
        try
        {
            var settings = new AutoTuneSettings();

            var axes = await GetParameterValueAsync(Parameters.AUTOTUNE_AXES);
            if (axes.HasValue) settings.AxesToTune = (AutoTuneAxes)(int)axes.Value;

            var aggr = await GetParameterValueAsync(Parameters.AUTOTUNE_AGGR);
            if (aggr.HasValue) settings.Aggressiveness = aggr.Value;

            var minD = await GetParameterValueAsync(Parameters.AUTOTUNE_MIN_D);
            if (minD.HasValue) settings.MinD = minD.Value;

            // Find which channel has AutoTune option (17)
            for (int ch = 5; ch <= 12; ch++)
            {
                var option = await GetParameterValueAsync(string.Format(Parameters.RC_OPTION_TEMPLATE, ch));
                if (option.HasValue && (int)option.Value == 17)
                {
                    settings.AutoTuneSwitch = (AutoTuneChannel)ch;
                    break;
                }
            }

            _logger.LogInformation("Loaded AutoTune settings");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AutoTune settings");
            return null;
        }
    }

    public async Task<bool> UpdateAutoTuneSettingsAsync(AutoTuneSettings settings)
    {
        try
        {
            var success = true;

            success &= await SetParameterValueAsync(Parameters.AUTOTUNE_AXES, (int)settings.AxesToTune);
            success &= await SetParameterValueAsync(Parameters.AUTOTUNE_AGGR, settings.Aggressiveness);
            success &= await SetParameterValueAsync(Parameters.AUTOTUNE_MIN_D, settings.MinD);

            // Set AutoTune switch channel
            if (settings.AutoTuneSwitch != AutoTuneChannel.None)
            {
                // First clear any existing AutoTune assignment
                for (int ch = 5; ch <= 12; ch++)
                {
                    var option = await GetParameterValueAsync(string.Format(Parameters.RC_OPTION_TEMPLATE, ch));
                    if (option.HasValue && (int)option.Value == 17)
                    {
                        await SetParameterValueAsync(string.Format(Parameters.RC_OPTION_TEMPLATE, ch), 0);
                    }
                }

                // Set new channel
                success &= await SetParameterValueAsync(
                    string.Format(Parameters.RC_OPTION_TEMPLATE, (int)settings.AutoTuneSwitch), 17);
            }

            _logger.LogInformation("Updated AutoTune settings, success={Success}", success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating AutoTune settings");
            return false;
        }
    }

    public Task<bool> SetAutoTuneAxesAsync(AutoTuneAxes axes)
    {
        return SetParameterValueAsync(Parameters.AUTOTUNE_AXES, (int)axes);
    }

    public Task<bool> SetAutoTuneAggressivenessAsync(float value)
    {
        return SetParameterValueAsync(Parameters.AUTOTUNE_AGGR, Math.Clamp(value, 0.05f, 0.2f));
    }

    public async Task<bool> SetAutoTuneSwitchChannelAsync(AutoTuneChannel channel)
    {
        if (channel == AutoTuneChannel.None)
            return true;

        return await SetParameterValueAsync(
            string.Format(Parameters.RC_OPTION_TEMPLATE, (int)channel), 17);
    }

    #endregion

    #region In-Flight Tuning

    public async Task<InFlightTuningSettings?> GetInFlightTuningSettingsAsync()
    {
        try
        {
            var settings = new InFlightTuningSettings();

            var tune = await GetParameterValueAsync(Parameters.TUNE);
            if (tune.HasValue) settings.TuneOption = (InFlightTuningOption)(int)tune.Value;

            var tuneMin = await GetParameterValueAsync(Parameters.TUNE_MIN);
            if (tuneMin.HasValue) settings.TuneMin = tuneMin.Value;

            var tuneMax = await GetParameterValueAsync(Parameters.TUNE_MAX);
            if (tuneMax.HasValue) settings.TuneMax = tuneMax.Value;

            _logger.LogInformation("Loaded in-flight tuning settings");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting in-flight tuning settings");
            return null;
        }
    }

    public async Task<bool> UpdateInFlightTuningSettingsAsync(InFlightTuningSettings settings)
    {
        try
        {
            var success = true;

            success &= await SetParameterValueAsync(Parameters.TUNE, (int)settings.TuneOption);
            success &= await SetParameterValueAsync(Parameters.TUNE_MIN, settings.TuneMin);
            success &= await SetParameterValueAsync(Parameters.TUNE_MAX, settings.TuneMax);

            _logger.LogInformation("Updated in-flight tuning settings, success={Success}", success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating in-flight tuning settings");
            return false;
        }
    }

    public Task<bool> SetTuneOptionAsync(InFlightTuningOption option)
    {
        return SetParameterValueAsync(Parameters.TUNE, (int)option);
    }

    public Task<bool> SetTuneMinAsync(float value)
    {
        return SetParameterValueAsync(Parameters.TUNE_MIN, value);
    }

    public Task<bool> SetTuneMaxAsync(float value)
    {
        return SetParameterValueAsync(Parameters.TUNE_MAX, value);
    }

    #endregion

    #region Full Configuration

    public async Task<PidTuningConfiguration?> GetFullConfigurationAsync()
    {
        try
        {
            var config = new PidTuningConfiguration();

            var basic = await GetBasicTuningSettingsAsync();
            if (basic != null) config.BasicTuning = basic;

            var roll = await GetAxisPidSettingsAsync(TuningAxis.Roll);
            if (roll != null) config.RollPid = roll;

            var pitch = await GetAxisPidSettingsAsync(TuningAxis.Pitch);
            if (pitch != null) config.PitchPid = pitch;

            var yaw = await GetAxisPidSettingsAsync(TuningAxis.Yaw);
            if (yaw != null) config.YawPid = yaw;

            var autoTune = await GetAutoTuneSettingsAsync();
            if (autoTune != null) config.AutoTune = autoTune;

            var inFlight = await GetInFlightTuningSettingsAsync();
            if (inFlight != null) config.InFlightTuning = inFlight;

            SettingsChanged?.Invoke(this, config);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting full PID configuration");
            return null;
        }
    }

    public async Task<bool> ApplyFullConfigurationAsync(PidTuningConfiguration config)
    {
        try
        {
            var success = true;

            success &= await UpdateBasicTuningSettingsAsync(config.BasicTuning);
            success &= await UpdateAxisPidSettingsAsync(config.RollPid);
            success &= await UpdateAxisPidSettingsAsync(config.PitchPid);
            success &= await UpdateAxisPidSettingsAsync(config.YawPid);
            success &= await UpdateAutoTuneSettingsAsync(config.AutoTune);
            success &= await UpdateInFlightTuningSettingsAsync(config.InFlightTuning);

            _logger.LogInformation("Applied full PID configuration, success={Success}", success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying full PID configuration");
            return false;
        }
    }

    public async Task<bool> ApplyDefaultConfigurationAsync()
    {
        var defaultConfig = new PidTuningConfiguration
        {
            BasicTuning = new BasicTuningSettings
            {
                RcFeelRollPitch = 0.15f,
                RollPitchSensitivity = 0.135f,
                ClimbSensitivity = 1.0f,
                SpinWhileArmed = 0.1f,
                MinThrottle = 0.15f
            },
            RollPid = new AxisPidSettings
            {
                Axis = TuningAxis.Roll,
                AngleP = 4.5f,
                RateP = 0.135f,
                RateI = 0.135f,
                RateD = 0.0036f,
                RateFF = 0.0f,
                RateFilter = 20.0f,
                RateIMax = 0.5f
            },
            PitchPid = new AxisPidSettings
            {
                Axis = TuningAxis.Pitch,
                AngleP = 4.5f,
                RateP = 0.135f,
                RateI = 0.135f,
                RateD = 0.0036f,
                RateFF = 0.0f,
                RateFilter = 20.0f,
                RateIMax = 0.5f
            },
            YawPid = new AxisPidSettings
            {
                Axis = TuningAxis.Yaw,
                AngleP = 4.5f,
                RateP = 0.18f,
                RateI = 0.018f,
                RateD = 0.0f,
                RateFF = 0.0f,
                RateFilter = 20.0f,
                RateIMax = 0.5f
            },
            AutoTune = new AutoTuneSettings
            {
                AxesToTune = AutoTuneAxes.Roll | AutoTuneAxes.Pitch | AutoTuneAxes.Yaw,
                Aggressiveness = 0.1f,
                MinD = 0.001f
            },
            InFlightTuning = new InFlightTuningSettings
            {
                TuneOption = InFlightTuningOption.None,
                TuneMin = 0,
                TuneMax = 0
            }
        };

        return await ApplyFullConfigurationAsync(defaultConfig);
    }

    public List<string> ValidateConfiguration(PidTuningConfiguration config)
    {
        var warnings = new List<string>();

        // Basic tuning validation
        if (config.BasicTuning.RcFeelRollPitch < 0.05f)
            warnings.Add("RC Feel is very low - may feel unresponsive");
        if (config.BasicTuning.RcFeelRollPitch > 0.5f)
            warnings.Add("RC Feel is very high - may feel too aggressive");

        // Rate P validation
        if (config.RollPid.RateP < 0.05f || config.PitchPid.RateP < 0.05f)
            warnings.Add("Rate P gain is very low - may cause sluggish response");
        if (config.RollPid.RateP > 0.3f || config.PitchPid.RateP > 0.3f)
            warnings.Add("Rate P gain is high - may cause oscillations");

        // D gain validation
        if (config.RollPid.RateD > 0.01f || config.PitchPid.RateD > 0.01f)
            warnings.Add("D gain is high - may amplify noise and cause vibrations");

        // I gain validation
        if (config.RollPid.RateI < config.RollPid.RateP * 0.5f)
            warnings.Add("Roll I gain may be too low relative to P gain");
        if (config.PitchPid.RateI < config.PitchPid.RateP * 0.5f)
            warnings.Add("Pitch I gain may be too low relative to P gain");

        // AutoTune validation
        if (config.AutoTune.AxesToTune == AutoTuneAxes.None)
            warnings.Add("No axes selected for AutoTune");

        return warnings;
    }

    #endregion

    #region Parameter Info

    public PidParameterInfo GetParameterInfo(string parameterName)
    {
        return _parameterInfoCache.TryGetValue(parameterName, out var info) ? info : new PidParameterInfo
        {
            ParameterName = parameterName,
            DisplayName = parameterName,
            Description = "Unknown parameter"
        };
    }

    public IEnumerable<PidParameterInfo> GetAllParameterInfo()
    {
        return _parameterInfoCache.Values;
    }

    public IEnumerable<(InFlightTuningOption Option, string Label, string Description)> GetInFlightTuningOptions()
    {
        return new[]
        {
            (InFlightTuningOption.None, "None", "Disable in-flight tuning"),
            (InFlightTuningOption.StabilizeRollPitchKp, "Stabilize Roll/Pitch Kp", "Angle mode P gain"),
            (InFlightTuningOption.StabilizeYawKp, "Stabilize Yaw Kp", "Yaw angle P gain"),
            (InFlightTuningOption.RatePitchRollKp, "Rate Roll/Pitch Kp", "Rate controller P gain"),
            (InFlightTuningOption.RatePitchRollKi, "Rate Roll/Pitch Ki", "Rate controller I gain"),
            (InFlightTuningOption.RatePitchRollKd, "Rate Roll/Pitch Kd", "Rate controller D gain"),
            (InFlightTuningOption.RateYawKp, "Rate Yaw Kp", "Yaw rate P gain"),
            (InFlightTuningOption.RateYawKd, "Rate Yaw Kd", "Yaw rate D gain"),
            (InFlightTuningOption.AltHoldKp, "Alt Hold Kp", "Altitude hold P gain"),
            (InFlightTuningOption.ThrottleAccelKp, "Throttle Accel Kp", "Throttle acceleration P gain"),
            (InFlightTuningOption.ThrottleAccelKi, "Throttle Accel Ki", "Throttle acceleration I gain"),
            (InFlightTuningOption.ThrottleAccelKd, "Throttle Accel Kd", "Throttle acceleration D gain"),
            (InFlightTuningOption.LoiterPosKp, "Loiter Pos Kp", "Loiter position P gain"),
            (InFlightTuningOption.LoiterRateKp, "Loiter Rate Kp", "Loiter rate P gain"),
            (InFlightTuningOption.LoiterRateKi, "Loiter Rate Ki", "Loiter rate I gain"),
            (InFlightTuningOption.LoiterRateKd, "Loiter Rate Kd", "Loiter rate D gain"),
            (InFlightTuningOption.WpSpeed, "WP Speed", "Waypoint navigation speed"),
            (InFlightTuningOption.RatePitchRollFilter, "Rate Filter", "Rate controller D-term filter"),
            (InFlightTuningOption.RateYawFilter, "Yaw Filter", "Yaw rate D-term filter")
        };
    }

    private static readonly Dictionary<string, PidParameterInfo> _parameterInfoCache = new()
    {
        [Parameters.ATC_INPUT_TC] = new PidParameterInfo
        {
            ParameterName = Parameters.ATC_INPUT_TC,
            DisplayName = "RC Feel Roll/Pitch",
            Description = "Controls responsiveness to stick inputs. Lower = softer, Higher = crisper",
            MinValue = 0.0f,
            MaxValue = 1.0f,
            DefaultValue = 0.15f,
            Increment = 0.01f,
            Unit = ""
        },
        [Parameters.ATC_ANG_RLL_P] = new PidParameterInfo
        {
            ParameterName = Parameters.ATC_ANG_RLL_P,
            DisplayName = "Roll Angle P",
            Description = "Angle controller P gain for roll axis",
            MinValue = 3.0f,
            MaxValue = 12.0f,
            DefaultValue = 4.5f,
            Increment = 0.1f,
            Unit = ""
        },
        [Parameters.ATC_RAT_RLL_P] = new PidParameterInfo
        {
            ParameterName = Parameters.ATC_RAT_RLL_P,
            DisplayName = "Roll Rate P",
            Description = "Rate controller P gain for roll axis",
            MinValue = 0.01f,
            MaxValue = 0.5f,
            DefaultValue = 0.135f,
            Increment = 0.001f,
            Unit = ""
        },
        [Parameters.ATC_RAT_RLL_I] = new PidParameterInfo
        {
            ParameterName = Parameters.ATC_RAT_RLL_I,
            DisplayName = "Roll Rate I",
            Description = "Rate controller I gain for roll axis",
            MinValue = 0.01f,
            MaxValue = 2.0f,
            DefaultValue = 0.135f,
            Increment = 0.001f,
            Unit = ""
        },
        [Parameters.ATC_RAT_RLL_D] = new PidParameterInfo
        {
            ParameterName = Parameters.ATC_RAT_RLL_D,
            DisplayName = "Roll Rate D",
            Description = "Rate controller D gain for roll axis",
            MinValue = 0.0f,
            MaxValue = 0.5f,
            DefaultValue = 0.0036f,
            Increment = 0.0001f,
            Unit = ""
        }
        // Additional parameters follow same pattern...
    };

    #endregion
}

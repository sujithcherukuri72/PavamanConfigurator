using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service implementation for RC calibration operations.
/// Handles RC channel calibration, monitoring, and attitude mapping via MAVLink.
/// </summary>
public class RcCalibrationService : IRcCalibrationService
{
    private readonly ILogger<RcCalibrationService> _logger;
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    private RcCalibrationState _currentState = RcCalibrationState.Idle;
    private readonly List<RcChannelValue> _liveChannelValues = new();
    private readonly object _lock = new();

    // ArduPilot parameter names
    private static class Parameters
    {
        // Channel configuration template (replace x with channel number 1-16)
        public const string RC_MIN_TEMPLATE = "RC{0}_MIN";
        public const string RC_MAX_TEMPLATE = "RC{0}_MAX";
        public const string RC_TRIM_TEMPLATE = "RC{0}_TRIM";
        public const string RC_DZ_TEMPLATE = "RC{0}_DZ";
        public const string RC_OPTION_TEMPLATE = "RC{0}_OPTION";
        public const string RC_REVERSED_TEMPLATE = "RC{0}_REVERSED";

        // Attitude channel mapping
        public const string RCMAP_ROLL = "RCMAP_ROLL";
        public const string RCMAP_PITCH = "RCMAP_PITCH";
        public const string RCMAP_THROTTLE = "RCMAP_THROTTLE";
        public const string RCMAP_YAW = "RCMAP_YAW";

        // RC options
        public const string RC_OPTIONS = "RC_OPTIONS";
    }

    public event EventHandler<RcChannelsUpdateEventArgs>? RcChannelsUpdated;
    public event EventHandler<RcCalibrationProgress>? CalibrationStateChanged;
    public event EventHandler<bool>? CalibrationCompleted;
    public event EventHandler<string>? ParameterUpdated;

    public RcCalibrationState CurrentState => _currentState;
    public bool IsCalibrating => _currentState == RcCalibrationState.CollectingMinMax ||
                                  _currentState == RcCalibrationState.WaitingForCenter ||
                                  _currentState == RcCalibrationState.WaitingToStart;

    public IReadOnlyList<RcChannelValue> LiveChannelValues
    {
        get
        {
            lock (_lock)
            {
                return _liveChannelValues.ToList();
            }
        }
    }

    public RcCalibrationService(
        ILogger<RcCalibrationService> logger,
        IParameterService parameterService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _parameterService = parameterService;
        _connectionService = connectionService;

        // Initialize 16 channel value holders
        for (int i = 1; i <= 16; i++)
        {
            _liveChannelValues.Add(new RcChannelValue { Channel = (RcChannel)i });
        }

        // Subscribe to RC channels from connection service
        _connectionService.RcChannelsReceived += OnRcChannelsReceived;
        _parameterService.ParameterUpdated += OnParameterUpdated;
    }

    private void OnRcChannelsReceived(object? sender, RcChannelsEventArgs e)
    {
        lock (_lock)
        {
            // Update live channel values
            UpdateChannelValue(0, e.Channel1);
            UpdateChannelValue(1, e.Channel2);
            UpdateChannelValue(2, e.Channel3);
            UpdateChannelValue(3, e.Channel4);
            UpdateChannelValue(4, e.Channel5);
            UpdateChannelValue(5, e.Channel6);
            UpdateChannelValue(6, e.Channel7);
            UpdateChannelValue(7, e.Channel8);

            // Channels 9-16 would come from RC_CHANNELS_RAW if available
            // For now, set them as 0 if not available
        }

        // Raise event
        RcChannelsUpdated?.Invoke(this, new RcChannelsUpdateEventArgs
        {
            Channels = _liveChannelValues.ToList(),
            ChannelCount = e.ChannelCount,
            Rssi = e.Rssi
        });
    }

    private void UpdateChannelValue(int index, ushort value)
    {
        if (index >= 0 && index < _liveChannelValues.Count)
        {
            _liveChannelValues[index].PwmValue = value;

            // Update min/max if calibrating
            if (IsCalibrating)
            {
                _liveChannelValues[index].UpdateMinMax(value);
            }
        }
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        if (parameterName.StartsWith("RC", StringComparison.OrdinalIgnoreCase) ||
            parameterName.StartsWith("RCMAP", StringComparison.OrdinalIgnoreCase))
        {
            ParameterUpdated?.Invoke(this, parameterName);
        }
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
            _logger.LogDebug("Set {Parameter} = {Value}", name, value);
        }
        else
        {
            _logger.LogWarning("Failed to set {Parameter} = {Value}", name, value);
        }
        return result;
    }

    private string GetMinParamName(int channel) => string.Format(Parameters.RC_MIN_TEMPLATE, channel);
    private string GetMaxParamName(int channel) => string.Format(Parameters.RC_MAX_TEMPLATE, channel);
    private string GetTrimParamName(int channel) => string.Format(Parameters.RC_TRIM_TEMPLATE, channel);
    private string GetDzParamName(int channel) => string.Format(Parameters.RC_DZ_TEMPLATE, channel);
    private string GetOptionParamName(int channel) => string.Format(Parameters.RC_OPTION_TEMPLATE, channel);
    private string GetReversedParamName(int channel) => string.Format(Parameters.RC_REVERSED_TEMPLATE, channel);

    #endregion

    #region Channel Configuration

    public async Task<RcCalibrationConfiguration?> GetRcConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Loading RC configuration from vehicle");

            var config = new RcCalibrationConfiguration();

            // Load each channel configuration
            for (int i = 1; i <= 16; i++)
            {
                var channelConfig = await GetChannelConfigAsync((RcChannel)i);
                if (channelConfig != null)
                {
                    var existing = config.Channels.FirstOrDefault(c => c.Channel == (RcChannel)i);
                    if (existing != null)
                    {
                        var index = config.Channels.IndexOf(existing);
                        config.Channels[index] = channelConfig;
                    }
                }
            }

            // Load attitude mapping
            var mapping = await GetAttitudeMappingAsync();
            if (mapping != null)
            {
                config.AttitudeMapping = mapping;
            }

            _logger.LogInformation("RC configuration loaded successfully");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading RC configuration");
            return null;
        }
    }

    public async Task<RcChannelConfig?> GetChannelConfigAsync(RcChannel channel)
    {
        try
        {
            var channelNum = (int)channel;
            var config = new RcChannelConfig { Channel = channel };

            var min = await GetParameterValueAsync(GetMinParamName(channelNum));
            if (min.HasValue) config.PwmMin = (int)min.Value;

            var max = await GetParameterValueAsync(GetMaxParamName(channelNum));
            if (max.HasValue) config.PwmMax = (int)max.Value;

            var trim = await GetParameterValueAsync(GetTrimParamName(channelNum));
            if (trim.HasValue) config.PwmTrim = (int)trim.Value;

            var dz = await GetParameterValueAsync(GetDzParamName(channelNum));
            if (dz.HasValue) config.DeadZone = (int)dz.Value;

            var option = await GetParameterValueAsync(GetOptionParamName(channelNum));
            if (option.HasValue) config.Option = (RcChannelOption)(int)option.Value;

            var reversed = await GetParameterValueAsync(GetReversedParamName(channelNum));
            if (reversed.HasValue) config.Reversed = (RcReversed)(int)reversed.Value;

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel {Channel} configuration", channel);
            return null;
        }
    }

    public async Task<bool> UpdateChannelConfigAsync(RcChannelConfig config)
    {
        try
        {
            var channelNum = (int)config.Channel;
            var success = true;

            success &= await SetParameterValueAsync(GetMinParamName(channelNum), config.PwmMin);
            success &= await SetParameterValueAsync(GetMaxParamName(channelNum), config.PwmMax);
            success &= await SetParameterValueAsync(GetTrimParamName(channelNum), config.PwmTrim);
            success &= await SetParameterValueAsync(GetDzParamName(channelNum), config.DeadZone);
            success &= await SetParameterValueAsync(GetReversedParamName(channelNum), (int)config.Reversed);

            _logger.LogInformation("Channel {Channel} config updated: Min={Min}, Max={Max}, Trim={Trim}",
                config.Channel, config.PwmMin, config.PwmMax, config.PwmTrim);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating channel {Channel} configuration", config.Channel);
            return false;
        }
    }

    public async Task<bool> UpdateRcConfigurationAsync(RcCalibrationConfiguration config)
    {
        try
        {
            _logger.LogInformation("Updating RC configuration");

            var success = true;

            // Update each channel
            foreach (var channelConfig in config.Channels)
            {
                success &= await UpdateChannelConfigAsync(channelConfig);
            }

            // Update attitude mapping
            success &= await UpdateAttitudeMappingAsync(config.AttitudeMapping);

            _logger.LogInformation("RC configuration update completed, success={Success}", success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RC configuration");
            return false;
        }
    }

    #endregion

    #region Calibration Operations

    public Task<bool> StartCalibrationAsync()
    {
        try
        {
            _logger.LogInformation("Starting RC calibration");

            // Reset min/max tracking for all channels
            lock (_lock)
            {
                foreach (var channel in _liveChannelValues)
                {
                    channel.ResetMinMax();
                }
            }

            _currentState = RcCalibrationState.CollectingMinMax;

            CalibrationStateChanged?.Invoke(this, new RcCalibrationProgress
            {
                State = _currentState,
                Instructions = "Move all sticks and switches to their extreme positions",
                StatusMessage = "Collecting RC input range data..."
            });

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting RC calibration");
            _currentState = RcCalibrationState.Failed;
            return Task.FromResult(false);
        }
    }

    public Task<bool> StopCalibrationAsync()
    {
        try
        {
            _logger.LogInformation("Stopping RC calibration");

            _currentState = RcCalibrationState.Cancelled;

            CalibrationStateChanged?.Invoke(this, new RcCalibrationProgress
            {
                State = _currentState,
                StatusMessage = "Calibration cancelled"
            });

            _currentState = RcCalibrationState.Idle;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping RC calibration");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> CompleteCalibrationAsync()
    {
        try
        {
            _logger.LogInformation("Completing RC calibration");

            // Validate that we have good data
            var channelValues = LiveChannelValues;
            var success = true;

            // Save calibration values for channels 1-8 (main control channels)
            for (int i = 0; i < 8 && i < channelValues.Count; i++)
            {
                var channel = channelValues[i];

                // Check if we got valid min/max data
                if (channel.MinSeen < channel.MaxSeen &&
                    channel.MinSeen > 800 && channel.MaxSeen < 2200)
                {
                    var range = channel.MaxSeen - channel.MinSeen;

                    // Only calibrate if we have a reasonable range
                    if (range >= RcCalibrationDefaults.MinimumValidRange)
                    {
                        var channelNum = i + 1;
                        var trim = (channel.MinSeen + channel.MaxSeen) / 2;

                        // For throttle (typically channel 3), trim should be at minimum
                        if (channelNum == 3)
                        {
                            trim = channel.MinSeen;
                        }

                        success &= await SetParameterValueAsync(GetMinParamName(channelNum), channel.MinSeen);
                        success &= await SetParameterValueAsync(GetMaxParamName(channelNum), channel.MaxSeen);
                        success &= await SetParameterValueAsync(GetTrimParamName(channelNum), trim);

                        _logger.LogInformation("Channel {Channel} calibrated: Min={Min}, Max={Max}, Trim={Trim}",
                            channelNum, channel.MinSeen, channel.MaxSeen, trim);
                    }
                    else
                    {
                        _logger.LogWarning("Channel {Channel} has insufficient range ({Range}us), skipping",
                            i + 1, range);
                    }
                }
            }

            _currentState = success ? RcCalibrationState.Completed : RcCalibrationState.Failed;

            CalibrationStateChanged?.Invoke(this, new RcCalibrationProgress
            {
                State = _currentState,
                StatusMessage = success ? "Calibration completed successfully" : "Calibration failed"
            });

            CalibrationCompleted?.Invoke(this, success);

            // Reset to idle after a short delay
            await Task.Delay(500);
            _currentState = RcCalibrationState.Idle;

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing RC calibration");
            _currentState = RcCalibrationState.Failed;
            CalibrationCompleted?.Invoke(this, false);
            return false;
        }
    }

    public async Task<bool> ResetCalibrationAsync()
    {
        _logger.LogInformation("Resetting RC calibration to defaults");
        return await ApplyPDRLDefaultsAsync();
    }

    #endregion

    #region Attitude Channel Mapping

    public async Task<AttitudeChannelMapping?> GetAttitudeMappingAsync()
    {
        try
        {
            var mapping = new AttitudeChannelMapping();

            var roll = await GetParameterValueAsync(Parameters.RCMAP_ROLL);
            if (roll.HasValue) mapping.RollChannel = (RcChannel)(int)roll.Value;

            var pitch = await GetParameterValueAsync(Parameters.RCMAP_PITCH);
            if (pitch.HasValue) mapping.PitchChannel = (RcChannel)(int)pitch.Value;

            var throttle = await GetParameterValueAsync(Parameters.RCMAP_THROTTLE);
            if (throttle.HasValue) mapping.ThrottleChannel = (RcChannel)(int)throttle.Value;

            var yaw = await GetParameterValueAsync(Parameters.RCMAP_YAW);
            if (yaw.HasValue) mapping.YawChannel = (RcChannel)(int)yaw.Value;

            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attitude mapping");
            return null;
        }
    }

    public async Task<bool> UpdateAttitudeMappingAsync(AttitudeChannelMapping mapping)
    {
        try
        {
            var success = true;

            success &= await SetParameterValueAsync(Parameters.RCMAP_ROLL, (int)mapping.RollChannel);
            success &= await SetParameterValueAsync(Parameters.RCMAP_PITCH, (int)mapping.PitchChannel);
            success &= await SetParameterValueAsync(Parameters.RCMAP_THROTTLE, (int)mapping.ThrottleChannel);
            success &= await SetParameterValueAsync(Parameters.RCMAP_YAW, (int)mapping.YawChannel);

            _logger.LogInformation("Attitude mapping updated: Roll=CH{Roll}, Pitch=CH{Pitch}, Throttle=CH{Throttle}, Yaw=CH{Yaw}",
                (int)mapping.RollChannel, (int)mapping.PitchChannel, (int)mapping.ThrottleChannel, (int)mapping.YawChannel);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attitude mapping");
            return false;
        }
    }

    public Task<bool> SetRollChannelAsync(RcChannel channel) =>
        SetParameterValueAsync(Parameters.RCMAP_ROLL, (int)channel);

    public Task<bool> SetPitchChannelAsync(RcChannel channel) =>
        SetParameterValueAsync(Parameters.RCMAP_PITCH, (int)channel);

    public Task<bool> SetThrottleChannelAsync(RcChannel channel) =>
        SetParameterValueAsync(Parameters.RCMAP_THROTTLE, (int)channel);

    public Task<bool> SetYawChannelAsync(RcChannel channel) =>
        SetParameterValueAsync(Parameters.RCMAP_YAW, (int)channel);

    #endregion

    #region Channel Options

    public async Task<bool> SetChannelOptionAsync(RcChannel channel, RcChannelOption option)
    {
        return await SetParameterValueAsync(GetOptionParamName((int)channel), (int)option);
    }

    public async Task<RcChannelOption> GetChannelOptionAsync(RcChannel channel)
    {
        var value = await GetParameterValueAsync(GetOptionParamName((int)channel));
        return value.HasValue ? (RcChannelOption)(int)value.Value : RcChannelOption.DoNothing;
    }

    public async Task<bool> SetChannelReversedAsync(RcChannel channel, bool reversed)
    {
        return await SetParameterValueAsync(GetReversedParamName((int)channel), reversed ? 1 : 0);
    }

    #endregion

    #region Validation

    public List<string> ValidateConfiguration(RcCalibrationConfiguration config)
    {
        return RcCalibrationDefaults.ValidateCalibration(config);
    }

    public async Task<bool> IsRcCalibratedAsync()
    {
        // Check if main channels have reasonable calibration values
        for (int i = 1; i <= 4; i++)
        {
            var min = await GetParameterValueAsync(GetMinParamName(i));
            var max = await GetParameterValueAsync(GetMaxParamName(i));

            if (!min.HasValue || !max.HasValue)
                return false;

            var range = max.Value - min.Value;
            if (range < RcCalibrationDefaults.MinimumValidRange)
                return false;
        }

        return true;
    }

    #endregion

    #region Defaults

    public async Task<bool> ApplyPDRLDefaultsAsync()
    {
        _logger.LogInformation("Applying PDRL-compliant RC defaults");

        var defaults = RcCalibrationDefaults.GetPDRLDefaults();
        return await UpdateRcConfigurationAsync(defaults);
    }

    public IEnumerable<(RcChannelOption Option, string Label, string Description)> GetChannelOptions()
    {
        return new[]
        {
            (RcChannelOption.DoNothing, "Do Nothing", "No function assigned"),
            (RcChannelOption.Flip, "Flip", "Flip mode (acrobatic)"),
            (RcChannelOption.SimpleMode, "Simple Mode", "Simple/beginner mode"),
            (RcChannelOption.RTL, "RTL", "Return to Launch"),
            (RcChannelOption.SaveTrim, "Save Trim", "Save current trim values"),
            (RcChannelOption.CameraTrigger, "Camera Trigger", "Trigger camera shutter"),
            (RcChannelOption.Fence, "Fence Enable", "Enable/disable geofence"),
            (RcChannelOption.AutoTune, "AutoTune", "Start/stop AutoTune"),
            (RcChannelOption.Land, "Land", "Land mode"),
            (RcChannelOption.ParachuteEnable, "Parachute Enable", "Enable parachute"),
            (RcChannelOption.ParachuteRelease, "Parachute Release", "Release parachute"),
            (RcChannelOption.PosHold, "PosHold", "Position Hold mode"),
            (RcChannelOption.AltHold, "AltHold", "Altitude Hold mode"),
            (RcChannelOption.Loiter, "Loiter", "Loiter mode"),
            (RcChannelOption.MotorInterlock, "Motor Interlock", "Motor safety interlock"),
            (RcChannelOption.Brake, "Brake", "Brake mode"),
            (RcChannelOption.MotorEStop, "Motor E-Stop", "Emergency motor stop"),
            (RcChannelOption.MotorEStopNonLatching, "Motor E-Stop (Non-Latching)", "Non-latching emergency stop"),
            (RcChannelOption.Stabilize, "Stabilize", "Stabilize mode"),
            (RcChannelOption.ArmDisarm, "Arm/Disarm", "Toggle arm/disarm"),
            (RcChannelOption.SmartRTL, "Smart RTL", "Smart Return to Launch"),
            (RcChannelOption.Auto, "Auto", "Auto/Mission mode"),
            (RcChannelOption.Guided, "Guided", "Guided mode"),
            (RcChannelOption.Circle, "Circle", "Circle mode"),
            (RcChannelOption.Acro, "Acro", "Acro mode"),
            (RcChannelOption.KillSwitch, "Kill Switch", "Kill switch")
        };
    }

    #endregion
}

using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Configuration for a single RC input channel.
/// Maps to ArduPilot RCx_ parameters.
/// </summary>
public class RcChannelConfig
{
    /// <summary>
    /// Channel index (1-16)
    /// </summary>
    public RcChannel Channel { get; set; }

    /// <summary>
    /// Minimum PWM value (RCx_MIN) - typically 1000
    /// </summary>
    public int PwmMin { get; set; } = 1000;

    /// <summary>
    /// Maximum PWM value (RCx_MAX) - typically 2000
    /// </summary>
    public int PwmMax { get; set; } = 2000;

    /// <summary>
    /// Trim/center PWM value (RCx_TRIM) - typically 1500
    /// </summary>
    public int PwmTrim { get; set; } = 1500;

    /// <summary>
    /// Dead zone around trim (RCx_DZ) - typically 30
    /// </summary>
    public int DeadZone { get; set; } = 30;

    /// <summary>
    /// Channel option/function (RCx_OPTION)
    /// </summary>
    public RcChannelOption Option { get; set; } = RcChannelOption.DoNothing;

    /// <summary>
    /// Whether channel is reversed (RCx_REVERSED)
    /// </summary>
    public RcReversed Reversed { get; set; } = RcReversed.Normal;

    /// <summary>
    /// Gets the parameter name for minimum PWM
    /// </summary>
    public string MinParameterName => $"RC{(int)Channel}_MIN";

    /// <summary>
    /// Gets the parameter name for maximum PWM
    /// </summary>
    public string MaxParameterName => $"RC{(int)Channel}_MAX";

    /// <summary>
    /// Gets the parameter name for trim PWM
    /// </summary>
    public string TrimParameterName => $"RC{(int)Channel}_TRIM";

    /// <summary>
    /// Gets the parameter name for dead zone
    /// </summary>
    public string DeadZoneParameterName => $"RC{(int)Channel}_DZ";

    /// <summary>
    /// Gets the parameter name for channel option
    /// </summary>
    public string OptionParameterName => $"RC{(int)Channel}_OPTION";

    /// <summary>
    /// Gets the parameter name for reversed setting
    /// </summary>
    public string ReversedParameterName => $"RC{(int)Channel}_REVERSED";
}

/// <summary>
/// Live RC channel value data received from MAVLink RC_CHANNELS message
/// </summary>
public class RcChannelValue
{
    /// <summary>
    /// Channel index (1-16)
    /// </summary>
    public RcChannel Channel { get; set; }

    /// <summary>
    /// Current PWM value (typically 1000-2000, 65535 if not available)
    /// </summary>
    public int PwmValue { get; set; }

    /// <summary>
    /// Minimum value seen during calibration
    /// </summary>
    public int MinSeen { get; set; } = int.MaxValue;

    /// <summary>
    /// Maximum value seen during calibration
    /// </summary>
    public int MaxSeen { get; set; } = int.MinValue;

    /// <summary>
    /// Whether this channel has valid data
    /// </summary>
    public bool IsValid => PwmValue != 0 && PwmValue != 65535;

    /// <summary>
    /// Normalized value (0-100%)
    /// </summary>
    public int NormalizedPercent => IsValid && PwmValue >= 1000 && PwmValue <= 2000
        ? (int)((PwmValue - 1000) / 10.0)
        : 0;

    /// <summary>
    /// Update min/max tracking during calibration
    /// </summary>
    public void UpdateMinMax(int value)
    {
        if (value > 800 && value < 2200) // Valid PWM range
        {
            if (value < MinSeen) MinSeen = value;
            if (value > MaxSeen) MaxSeen = value;
        }
    }

    /// <summary>
    /// Reset min/max tracking
    /// </summary>
    public void ResetMinMax()
    {
        MinSeen = int.MaxValue;
        MaxSeen = int.MinValue;
    }
}

/// <summary>
/// Attitude channel mapping configuration.
/// Maps control functions to RC channels via RCMAP_ parameters.
/// </summary>
public class AttitudeChannelMapping
{
    /// <summary>
    /// Channel for roll control (RCMAP_ROLL) - typically Channel 1
    /// </summary>
    public RcChannel RollChannel { get; set; } = RcChannel.Channel1;

    /// <summary>
    /// Channel for pitch control (RCMAP_PITCH) - typically Channel 2
    /// </summary>
    public RcChannel PitchChannel { get; set; } = RcChannel.Channel2;

    /// <summary>
    /// Channel for throttle control (RCMAP_THROTTLE) - typically Channel 3
    /// </summary>
    public RcChannel ThrottleChannel { get; set; } = RcChannel.Channel3;

    /// <summary>
    /// Channel for yaw control (RCMAP_YAW) - typically Channel 4
    /// </summary>
    public RcChannel YawChannel { get; set; } = RcChannel.Channel4;
}

/// <summary>
/// Complete RC calibration configuration
/// </summary>
public class RcCalibrationConfiguration
{
    /// <summary>
    /// Configuration for all 16 channels
    /// </summary>
    public List<RcChannelConfig> Channels { get; set; } = new();

    /// <summary>
    /// Attitude function to channel mapping
    /// </summary>
    public AttitudeChannelMapping AttitudeMapping { get; set; } = new();

    /// <summary>
    /// Whether RC calibration has been completed
    /// </summary>
    public bool IsCalibrated { get; set; }

    /// <summary>
    /// Number of active RC channels detected
    /// </summary>
    public int ActiveChannelCount { get; set; } = 8;

    /// <summary>
    /// RSSI (signal strength) value
    /// </summary>
    public byte Rssi { get; set; }

    /// <summary>
    /// Initialize with default 16 channels
    /// </summary>
    public RcCalibrationConfiguration()
    {
        for (int i = 1; i <= 16; i++)
        {
            Channels.Add(new RcChannelConfig { Channel = (RcChannel)i });
        }
    }

    /// <summary>
    /// Get configuration for a specific channel
    /// </summary>
    public RcChannelConfig? GetChannel(RcChannel channel)
    {
        return Channels.FirstOrDefault(c => c.Channel == channel);
    }

    /// <summary>
    /// Get configuration for a specific channel by index
    /// </summary>
    public RcChannelConfig? GetChannel(int channelIndex)
    {
        return Channels.FirstOrDefault(c => (int)c.Channel == channelIndex);
    }
}

/// <summary>
/// RC calibration progress and state information
/// </summary>
public class RcCalibrationProgress
{
    /// <summary>
    /// Current calibration state
    /// </summary>
    public RcCalibrationState State { get; set; } = RcCalibrationState.Idle;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Current instruction text for user
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Status message
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Whether calibration is in progress
    /// </summary>
    public bool IsCalibrating => State == RcCalibrationState.CollectingMinMax ||
                                  State == RcCalibrationState.WaitingForCenter ||
                                  State == RcCalibrationState.WaitingToStart;

    /// <summary>
    /// Number of channels that have been successfully calibrated
    /// </summary>
    public int CalibratedChannelCount { get; set; }

    /// <summary>
    /// Total channels being calibrated
    /// </summary>
    public int TotalChannelCount { get; set; } = 4; // Roll, Pitch, Yaw, Throttle
}

/// <summary>
/// PDRL-compliant RC calibration defaults
/// </summary>
public static class RcCalibrationDefaults
{
    /// <summary>
    /// Standard PWM minimum value
    /// </summary>
    public const int StandardPwmMin = 1000;

    /// <summary>
    /// Standard PWM maximum value
    /// </summary>
    public const int StandardPwmMax = 2000;

    /// <summary>
    /// Standard PWM trim/center value
    /// </summary>
    public const int StandardPwmTrim = 1500;

    /// <summary>
    /// Default dead zone value
    /// </summary>
    public const int DefaultDeadZone = 30;

    /// <summary>
    /// Minimum acceptable range for valid calibration
    /// </summary>
    public const int MinimumValidRange = 400;

    /// <summary>
    /// Throttle dead zone (typically 0 for throttle)
    /// </summary>
    public const int ThrottleDeadZone = 0;

    /// <summary>
    /// Gets PDRL-compliant default configuration
    /// </summary>
    public static RcCalibrationConfiguration GetPDRLDefaults()
    {
        var config = new RcCalibrationConfiguration
        {
            AttitudeMapping = new AttitudeChannelMapping
            {
                RollChannel = RcChannel.Channel1,
                PitchChannel = RcChannel.Channel2,
                ThrottleChannel = RcChannel.Channel3,
                YawChannel = RcChannel.Channel4
            }
        };

        // Set standard values for all channels
        foreach (var channel in config.Channels)
        {
            channel.PwmMin = StandardPwmMin;
            channel.PwmMax = StandardPwmMax;
            channel.PwmTrim = StandardPwmTrim;
            channel.DeadZone = DefaultDeadZone;
            channel.Reversed = RcReversed.Normal;
        }

        // Throttle has no dead zone
        var throttle = config.GetChannel(RcChannel.Channel3);
        if (throttle != null)
        {
            throttle.DeadZone = ThrottleDeadZone;
        }

        return config;
    }

    /// <summary>
    /// Validate calibration values
    /// </summary>
    public static List<string> ValidateCalibration(RcCalibrationConfiguration config)
    {
        var warnings = new List<string>();

        // Check main control channels have valid ranges
        var rollChannel = config.GetChannel(config.AttitudeMapping.RollChannel);
        var pitchChannel = config.GetChannel(config.AttitudeMapping.PitchChannel);
        var throttleChannel = config.GetChannel(config.AttitudeMapping.ThrottleChannel);
        var yawChannel = config.GetChannel(config.AttitudeMapping.YawChannel);

        if (rollChannel != null && (rollChannel.PwmMax - rollChannel.PwmMin) < MinimumValidRange)
            warnings.Add("Roll channel has insufficient range - check transmitter settings");

        if (pitchChannel != null && (pitchChannel.PwmMax - pitchChannel.PwmMin) < MinimumValidRange)
            warnings.Add("Pitch channel has insufficient range - check transmitter settings");

        if (throttleChannel != null && (throttleChannel.PwmMax - throttleChannel.PwmMin) < MinimumValidRange)
            warnings.Add("Throttle channel has insufficient range - check transmitter settings");

        if (yawChannel != null && (yawChannel.PwmMax - yawChannel.PwmMin) < MinimumValidRange)
            warnings.Add("Yaw channel has insufficient range - check transmitter settings");

        // Check for channel mapping conflicts
        var mappedChannels = new[]
        {
            config.AttitudeMapping.RollChannel,
            config.AttitudeMapping.PitchChannel,
            config.AttitudeMapping.ThrottleChannel,
            config.AttitudeMapping.YawChannel
        };

        if (mappedChannels.Distinct().Count() != 4)
            warnings.Add("Attitude channels have duplicate mappings - each function must use a unique channel");

        return warnings;
    }
}

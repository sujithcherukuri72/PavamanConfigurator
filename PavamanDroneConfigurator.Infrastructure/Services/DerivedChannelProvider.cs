using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Infrastructure.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Provides derived/computed channels from raw log data.
/// Implements common derived values like VibeMag, GroundSpeed, BatteryPower.
/// </summary>
public class DerivedChannelProvider : IDerivedChannelProvider
{
    private readonly ILogger<DerivedChannelProvider> _logger;
    private readonly DataFlashLogParser? _parser;
    private readonly Dictionary<string, DerivedChannelDefinition> _channels;

    public DerivedChannelProvider(ILogger<DerivedChannelProvider> logger)
    {
        _logger = logger;
        _channels = BuildChannelDefinitions();
    }

    /// <summary>
    /// Sets the parser to use for computing derived channels.
    /// </summary>
    public void SetParser(DataFlashLogParser parser)
    {
        // Store reference for computation
        _parserRef = parser;
    }

    private DataFlashLogParser? _parserRef;

    private Dictionary<string, DerivedChannelDefinition> BuildChannelDefinitions()
    {
        return new Dictionary<string, DerivedChannelDefinition>
        {
            // Vibration Magnitude
            ["DERIVED.VibeMag"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.VibeMag",
                DisplayName = "Vibration Magnitude",
                Description = "Combined vibration magnitude from all axes: sqrt(VibeX² + VibeY² + VibeZ²)",
                Unit = "m/s²",
                Category = "Vibration",
                SourceFields = new[] { "VIBE.VibeX", "VIBE.VibeY", "VIBE.VibeZ" },
                ComputeFunction = values =>
                {
                    if (values.TryGetValue("VIBE.VibeX", out var x) &&
                        values.TryGetValue("VIBE.VibeY", out var y) &&
                        values.TryGetValue("VIBE.VibeZ", out var z))
                    {
                        return Math.Sqrt(x * x + y * y + z * z);
                    }
                    return double.NaN;
                }
            },

            // Ground Speed from GPS
            ["DERIVED.GroundSpeed"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.GroundSpeed",
                DisplayName = "Ground Speed",
                Description = "Ground speed from GPS: sqrt(VelN² + VelE²)",
                Unit = "m/s",
                Category = "Navigation",
                SourceFields = new[] { "GPS.VelN", "GPS.VelE" },
                ComputeFunction = values =>
                {
                    // Try NED velocities first
                    if (values.TryGetValue("GPS.VelN", out var vn) &&
                        values.TryGetValue("GPS.VelE", out var ve))
                    {
                        return Math.Sqrt(vn * vn + ve * ve);
                    }
                    // Fallback to Spd field
                    if (values.TryGetValue("GPS.Spd", out var spd))
                    {
                        return spd;
                    }
                    return double.NaN;
                }
            },

            // Ground Speed in km/h
            ["DERIVED.GroundSpeedKmh"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.GroundSpeedKmh",
                DisplayName = "Ground Speed (km/h)",
                Description = "Ground speed in kilometers per hour",
                Unit = "km/h",
                Category = "Navigation",
                SourceFields = new[] { "GPS.VelN", "GPS.VelE", "GPS.Spd" },
                ComputeFunction = values =>
                {
                    double speedMs = 0;
                    if (values.TryGetValue("GPS.VelN", out var vn) &&
                        values.TryGetValue("GPS.VelE", out var ve))
                    {
                        speedMs = Math.Sqrt(vn * vn + ve * ve);
                    }
                    else if (values.TryGetValue("GPS.Spd", out var spd))
                    {
                        speedMs = spd;
                    }
                    else
                    {
                        return double.NaN;
                    }
                    return speedMs * 3.6; // Convert m/s to km/h
                }
            },

            // Battery Power
            ["DERIVED.BatteryPower"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.BatteryPower",
                DisplayName = "Battery Power",
                Description = "Battery power consumption: Voltage × Current",
                Unit = "W",
                Category = "Power",
                SourceFields = new[] { "BAT.Volt", "BAT.Curr" },
                ComputeFunction = values =>
                {
                    if (values.TryGetValue("BAT.Volt", out var volt) &&
                        values.TryGetValue("BAT.Curr", out var curr))
                    {
                        return volt * curr;
                    }
                    // Try alternative field names
                    if (values.TryGetValue("CURR.Volt", out volt) &&
                        values.TryGetValue("CURR.Curr", out curr))
                    {
                        return volt * curr;
                    }
                    return double.NaN;
                }
            },

            // Battery Energy Consumed
            ["DERIVED.EnergyConsumed"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.EnergyConsumed",
                DisplayName = "Energy Consumed",
                Description = "Cumulative energy consumed from battery",
                Unit = "Wh",
                Category = "Power",
                SourceFields = new[] { "BAT.CurrTot" },
                ComputeFunction = values =>
                {
                    if (values.TryGetValue("BAT.CurrTot", out var mah))
                    {
                        // Assuming nominal voltage of 11.1V for 3S
                        return mah * 11.1 / 1000.0;
                    }
                    return double.NaN;
                }
            },

            // Vertical Speed from barometer
            ["DERIVED.VerticalSpeed"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.VerticalSpeed",
                DisplayName = "Vertical Speed",
                Description = "Vertical speed from barometer altitude rate",
                Unit = "m/s",
                Category = "Navigation",
                SourceFields = new[] { "BARO.Alt" },
                ComputeFunction = null // Requires derivative calculation
            },

            // Roll Rate
            ["DERIVED.RollRate"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.RollRate",
                DisplayName = "Roll Rate",
                Description = "Angular roll rate from IMU",
                Unit = "deg/s",
                Category = "Attitude",
                SourceFields = new[] { "IMU.GyrX" },
                ComputeFunction = values =>
                {
                    if (values.TryGetValue("IMU.GyrX", out var gyrx))
                    {
                        return gyrx * 180.0 / Math.PI; // Convert rad/s to deg/s
                    }
                    return double.NaN;
                }
            },

            // Pitch Rate
            ["DERIVED.PitchRate"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.PitchRate",
                DisplayName = "Pitch Rate",
                Description = "Angular pitch rate from IMU",
                Unit = "deg/s",
                Category = "Attitude",
                SourceFields = new[] { "IMU.GyrY" },
                ComputeFunction = values =>
                {
                    if (values.TryGetValue("IMU.GyrY", out var gyry))
                    {
                        return gyry * 180.0 / Math.PI;
                    }
                    return double.NaN;
                }
            },

            // Yaw Rate
            ["DERIVED.YawRate"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.YawRate",
                DisplayName = "Yaw Rate",
                Description = "Angular yaw rate from IMU",
                Unit = "deg/s",
                Category = "Attitude",
                SourceFields = new[] { "IMU.GyrZ" },
                ComputeFunction = values =>
                {
                    if (values.TryGetValue("IMU.GyrZ", out var gyrz))
                    {
                        return gyrz * 180.0 / Math.PI;
                    }
                    return double.NaN;
                }
            },

            // Accelerometer Magnitude
            ["DERIVED.AccelMag"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.AccelMag",
                DisplayName = "Accelerometer Magnitude",
                Description = "Combined acceleration magnitude: sqrt(AccX² + AccY² + AccZ²)",
                Unit = "m/s²",
                Category = "IMU",
                SourceFields = new[] { "IMU.AccX", "IMU.AccY", "IMU.AccZ" },
                ComputeFunction = values =>
                {
                    if (values.TryGetValue("IMU.AccX", out var x) &&
                        values.TryGetValue("IMU.AccY", out var y) &&
                        values.TryGetValue("IMU.AccZ", out var z))
                    {
                        return Math.Sqrt(x * x + y * y + z * z);
                    }
                    return double.NaN;
                }
            },

            // Distance from Home
            ["DERIVED.DistanceFromHome"] = new DerivedChannelDefinition
            {
                Key = "DERIVED.DistanceFromHome",
                DisplayName = "Distance from Home",
                Description = "Horizontal distance from home position",
                Unit = "m",
                Category = "Navigation",
                SourceFields = new[] { "GPS.Lat", "GPS.Lng" },
                ComputeFunction = null // Requires home position reference
            }
        };
    }

    public IReadOnlyList<DerivedChannelDefinition> GetDerivedChannels()
    {
        return _channels.Values.Where(c => c.ComputeFunction != null).ToList();
    }

    public bool IsDerivedChannel(string channelKey)
    {
        return channelKey.StartsWith("DERIVED.", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetSourceFields(string channelKey)
    {
        if (_channels.TryGetValue(channelKey, out var def))
        {
            return def.SourceFields.ToList();
        }
        return Array.Empty<string>();
    }

    public async Task<List<LogDataPoint>> ComputeChannelAsync(
        string channelKey,
        CancellationToken cancellationToken = default)
    {
        if (_parserRef == null)
        {
            _logger.LogWarning("No parser set for derived channel computation");
            return new List<LogDataPoint>();
        }

        if (!_channels.TryGetValue(channelKey, out var definition))
        {
            _logger.LogWarning("Unknown derived channel: {Key}", channelKey);
            return new List<LogDataPoint>();
        }

        if (definition.ComputeFunction == null)
        {
            _logger.LogWarning("Derived channel {Key} has no compute function", channelKey);
            return new List<LogDataPoint>();
        }

        return await Task.Run(() =>
        {
            try
            {
                return ComputeChannelInternal(definition, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing derived channel: {Key}", channelKey);
                return new List<LogDataPoint>();
            }
        }, cancellationToken);
    }

    private List<LogDataPoint> ComputeChannelInternal(
        DerivedChannelDefinition definition,
        CancellationToken cancellationToken)
    {
        var result = new List<LogDataPoint>();
        var sourceData = new Dictionary<string, List<LogDataPoint>>();

        // Load source data
        foreach (var sourceKey in definition.SourceFields)
        {
            var parts = sourceKey.Split('.');
            if (parts.Length == 2)
            {
                var data = _parserRef!.GetDataSeries(parts[0], parts[1]);
                if (data != null && data.Count > 0)
                {
                    sourceData[sourceKey] = data;
                }
            }
        }

        if (sourceData.Count == 0)
        {
            _logger.LogDebug("No source data available for derived channel: {Key}", definition.Key);
            return result;
        }

        // Find common time base (use first source as reference)
        var primarySource = sourceData.Values.First();
        var otherSources = sourceData.Where(kv => kv.Value != primarySource).ToList();

        // Build lookup indices for other sources
        var sourceIndices = new Dictionary<string, int>();
        foreach (var kv in otherSources)
        {
            sourceIndices[kv.Key] = 0;
        }

        // Compute derived values
        for (int i = 0; i < primarySource.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var timestamp = primarySource[i].Timestamp;
            var values = new Dictionary<string, double>();

            // Add primary source value
            var primaryKey = sourceData.First().Key;
            values[primaryKey] = primarySource[i].Value;

            // Find corresponding values in other sources (nearest neighbor)
            foreach (var kv in otherSources)
            {
                var source = kv.Value;
                var idx = sourceIndices[kv.Key];

                // Advance index to find nearest timestamp
                while (idx < source.Count - 1 && source[idx + 1].Timestamp <= timestamp)
                {
                    idx++;
                }
                sourceIndices[kv.Key] = idx;

                if (idx < source.Count)
                {
                    values[kv.Key] = source[idx].Value;
                }
            }

            // Compute derived value
            var computedValue = definition.ComputeFunction!(values);
            if (!double.IsNaN(computedValue) && !double.IsInfinity(computedValue))
            {
                result.Add(new LogDataPoint
                {
                    Index = i,
                    Timestamp = timestamp,
                    Value = computedValue
                });
            }
        }

        _logger.LogDebug("Computed {Count} points for derived channel: {Key}", result.Count, definition.Key);
        return result;
    }
}

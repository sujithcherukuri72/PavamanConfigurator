using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Infrastructure.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Query engine for efficient log data retrieval with decimation support.
/// Provides time-windowed queries with automatic downsampling for performance.
/// </summary>
public class LogQueryEngine : ILogQueryEngine
{
    private readonly ILogger<LogQueryEngine> _logger;
    private readonly IDerivedChannelProvider _derivedChannelProvider;
    
    private DataFlashLogParser? _parser;
    private ParsedLog? _parsedLog;
    private readonly Dictionary<string, (double[] Times, double[] Values)> _seriesCache = new();
    private readonly object _cacheLock = new();

    public LogQueryEngine(
        ILogger<LogQueryEngine> logger,
        IDerivedChannelProvider derivedChannelProvider)
    {
        _logger = logger;
        _derivedChannelProvider = derivedChannelProvider;
    }

    public bool IsLogLoaded => _parsedLog?.IsSuccess == true;

    /// <summary>
    /// Sets the parsed log data for querying.
    /// </summary>
    public void SetLogData(DataFlashLogParser parser, ParsedLog parsedLog)
    {
        _parser = parser;
        _parsedLog = parsedLog;
        
        lock (_cacheLock)
        {
            _seriesCache.Clear();
        }

        if (_derivedChannelProvider is DerivedChannelProvider dcp)
        {
            dcp.SetParser(parser);
        }

        _logger.LogInformation("Log query engine initialized with {Count} message types", 
            parsedLog.UniqueMessageTypes);
    }

    public (double StartTime, double EndTime) GetTimeRange()
    {
        if (_parsedLog == null)
            return (0, 0);

        return (
            _parsedLog.StartTime.TotalSeconds,
            _parsedLog.EndTime.TotalSeconds
        );
    }

    public IReadOnlyList<SeriesInfo> GetAvailableSeries()
    {
        var result = new List<SeriesInfo>();

        if (_parser == null)
            return result;

        // Add regular series from parser
        foreach (var seriesKey in _parser.GetAvailableDataSeries())
        {
            var parts = seriesKey.Split('.');
            if (parts.Length >= 2)
            {
                var dataPoints = _parser.GetDataSeries(parts[0], parts[1]);
                result.Add(new SeriesInfo
                {
                    Key = seriesKey,
                    MessageType = parts[0],
                    FieldName = parts[1],
                    DisplayName = seriesKey,
                    IsDerived = false,
                    DataPointCount = dataPoints?.Count ?? 0
                });
            }
        }

        // Add derived channels
        foreach (var derived in _derivedChannelProvider.GetDerivedChannels())
        {
            result.Add(new SeriesInfo
            {
                Key = derived.Key,
                MessageType = "DERIVED",
                FieldName = derived.DisplayName,
                DisplayName = derived.DisplayName,
                Unit = derived.Unit,
                IsDerived = true,
                DataPointCount = 0 // Computed on demand
            });
        }

        return result;
    }

    public async Task<DecimatedSeries> GetDecimatedSeriesAsync(
        string seriesKey,
        double? startTime,
        double? endTime,
        int targetPointCount,
        CancellationToken cancellationToken = default)
    {
        if (_parser == null || _parsedLog == null)
        {
            return new DecimatedSeries { SeriesKey = seriesKey, IsComplete = false };
        }

        return await Task.Run(async () =>
        {
            try
            {
                // Get or compute raw data
                var (times, values) = await GetRawSeriesAsync(seriesKey, cancellationToken);

                if (times.Length == 0)
                {
                    return new DecimatedSeries { SeriesKey = seriesKey, IsComplete = true };
                }

                // Apply time window filter
                if (startTime.HasValue || endTime.HasValue)
                {
                    var start = startTime ?? 0;
                    var end = endTime ?? double.MaxValue;
                    var filtered = FilterByTimeRange(times, values, start, end);
                    times = filtered.Times;
                    values = filtered.Values;
                }

                if (times.Length == 0)
                {
                    return new DecimatedSeries { SeriesKey = seriesKey, IsComplete = true };
                }

                // Calculate statistics before decimation
                var stats = LttbDecimation.CalculateStatistics(values);

                // Decimate if needed
                double[] decimatedTimes, decimatedValues;
                if (times.Length > targetPointCount && targetPointCount > 2)
                {
                    (decimatedTimes, decimatedValues) = LttbDecimation.Decimate(times, values, targetPointCount);
                }
                else
                {
                    decimatedTimes = times;
                    decimatedValues = values;
                }

                return new DecimatedSeries
                {
                    SeriesKey = seriesKey,
                    Times = decimatedTimes,
                    Values = decimatedValues,
                    OriginalPointCount = times.Length,
                    MinValue = stats.Min,
                    MaxValue = stats.Max,
                    MeanValue = stats.Mean,
                    IsComplete = true
                };
            }
            catch (OperationCanceledException)
            {
                return new DecimatedSeries { SeriesKey = seriesKey, IsComplete = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting decimated series: {Key}", seriesKey);
                return new DecimatedSeries { SeriesKey = seriesKey, IsComplete = false };
            }
        }, cancellationToken);
    }

    public async Task<Dictionary<string, DecimatedSeries>> GetMultipleSeriesAsync(
        IEnumerable<string> seriesKeys,
        double? startTime,
        double? endTime,
        int targetPointCount,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, DecimatedSeries>();
        var tasks = new List<Task<(string Key, DecimatedSeries Series)>>();

        foreach (var key in seriesKeys)
        {
            var capturedKey = key;
            tasks.Add(Task.Run(async () =>
            {
                var series = await GetDecimatedSeriesAsync(
                    capturedKey, startTime, endTime, targetPointCount, cancellationToken);
                return (capturedKey, series);
            }, cancellationToken));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var (key, series) in results)
        {
            result[key] = series;
        }

        return result;
    }

    public async Task<Dictionary<string, double?>> GetValuesAtTimeAsync(
        IEnumerable<string> seriesKeys,
        double timestamp,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, double?>();

        foreach (var key in seriesKeys)
        {
            try
            {
                var (times, values) = await GetRawSeriesAsync(key, cancellationToken);
                
                if (times.Length == 0)
                {
                    result[key] = null;
                    continue;
                }

                // Binary search for nearest time
                var index = Array.BinarySearch(times, timestamp);
                if (index < 0)
                {
                    index = ~index;
                    if (index >= times.Length) index = times.Length - 1;
                    if (index > 0)
                    {
                        // Check which neighbor is closer
                        if (Math.Abs(times[index - 1] - timestamp) < Math.Abs(times[index] - timestamp))
                            index--;
                    }
                }

                result[key] = values[index];
            }
            catch
            {
                result[key] = null;
            }
        }

        return result;
    }

    private async Task<(double[] Times, double[] Values)> GetRawSeriesAsync(
        string seriesKey,
        CancellationToken cancellationToken)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_seriesCache.TryGetValue(seriesKey, out var cached))
            {
                return cached;
            }
        }

        double[] times;
        double[] values;

        if (_derivedChannelProvider.IsDerivedChannel(seriesKey))
        {
            // Compute derived channel
            var dataPoints = await _derivedChannelProvider.ComputeChannelAsync(seriesKey, cancellationToken);
            times = dataPoints.Select(p => p.Timestamp / 1e6).ToArray(); // Convert microseconds to seconds
            values = dataPoints.Select(p => p.Value).ToArray();
        }
        else
        {
            // Get regular series from parser
            var parts = seriesKey.Split('.');
            if (parts.Length < 2 || _parser == null)
            {
                return (Array.Empty<double>(), Array.Empty<double>());
            }

            var dataPoints = _parser.GetDataSeries(parts[0], parts[1]);
            if (dataPoints == null || dataPoints.Count == 0)
            {
                return (Array.Empty<double>(), Array.Empty<double>());
            }

            times = dataPoints.Select(p => p.Timestamp / 1e6).ToArray(); // Convert microseconds to seconds
            values = dataPoints.Select(p => p.Value).ToArray();
        }

        // Cache the result
        lock (_cacheLock)
        {
            _seriesCache[seriesKey] = (times, values);
        }

        return (times, values);
    }

    private static (double[] Times, double[] Values) FilterByTimeRange(
        double[] times,
        double[] values,
        double startTime,
        double endTime)
    {
        var startIdx = Array.BinarySearch(times, startTime);
        if (startIdx < 0) startIdx = ~startIdx;

        var endIdx = Array.BinarySearch(times, endTime);
        if (endIdx < 0) endIdx = ~endIdx;

        if (startIdx >= endIdx)
            return (Array.Empty<double>(), Array.Empty<double>());

        var length = endIdx - startIdx;
        var filteredTimes = new double[length];
        var filteredValues = new double[length];

        Array.Copy(times, startIdx, filteredTimes, 0, length);
        Array.Copy(values, startIdx, filteredValues, 0, length);

        return (filteredTimes, filteredValues);
    }

    /// <summary>
    /// Clears the series cache. Call when loading a new log.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _seriesCache.Clear();
        }
    }
}

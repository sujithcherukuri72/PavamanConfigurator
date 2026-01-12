using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Query engine for efficient log data retrieval with decimation support.
/// Provides time-windowed queries with automatic downsampling for performance.
/// </summary>
public interface ILogQueryEngine
{
    /// <summary>
    /// Gets a decimated data series for plotting.
    /// Uses LTTB (Largest Triangle Three Buckets) algorithm for visual fidelity.
    /// </summary>
    /// <param name="seriesKey">Series key in format "MessageType.FieldName"</param>
    /// <param name="startTime">Start time in seconds (null for beginning)</param>
    /// <param name="endTime">End time in seconds (null for end)</param>
    /// <param name="targetPointCount">Target number of points for display</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decimated data points</returns>
    Task<DecimatedSeries> GetDecimatedSeriesAsync(
        string seriesKey,
        double? startTime,
        double? endTime,
        int targetPointCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple series in parallel for efficient multi-series plotting.
    /// </summary>
    Task<Dictionary<string, DecimatedSeries>> GetMultipleSeriesAsync(
        IEnumerable<string> seriesKeys,
        double? startTime,
        double? endTime,
        int targetPointCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the value at a specific timestamp for cursor readout.
    /// </summary>
    Task<Dictionary<string, double?>> GetValuesAtTimeAsync(
        IEnumerable<string> seriesKeys,
        double timestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the time range of the loaded log.
    /// </summary>
    (double StartTime, double EndTime) GetTimeRange();

    /// <summary>
    /// Gets all available series keys including derived channels.
    /// </summary>
    IReadOnlyList<SeriesInfo> GetAvailableSeries();

    /// <summary>
    /// Checks if a log is currently loaded.
    /// </summary>
    bool IsLogLoaded { get; }
}

/// <summary>
/// Decimated series data for efficient rendering.
/// </summary>
public class DecimatedSeries
{
    public string SeriesKey { get; set; } = string.Empty;
    public double[] Times { get; set; } = Array.Empty<double>();
    public double[] Values { get; set; } = Array.Empty<double>();
    public int OriginalPointCount { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double MeanValue { get; set; }
    public bool IsComplete { get; set; } = true;
}

/// <summary>
/// Information about an available series.
/// </summary>
public class SeriesInfo
{
    public string Key { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsDerived { get; set; }
    public int DataPointCount { get; set; }
}

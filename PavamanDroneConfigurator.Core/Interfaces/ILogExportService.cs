using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service for exporting log data to various formats.
/// </summary>
public interface ILogExportService
{
    /// <summary>
    /// Exports selected series to CSV format.
    /// </summary>
    /// <param name="filePath">Destination file path</param>
    /// <param name="seriesKeys">Series to export</param>
    /// <param name="startTime">Start time in seconds (null for beginning)</param>
    /// <param name="endTime">End time in seconds (null for end)</param>
    /// <param name="progress">Progress callback (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ExportResult> ExportToCsvAsync(
        string filePath,
        IEnumerable<string> seriesKeys,
        double? startTime,
        double? endTime,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports GPS track to KML format for Google Earth.
    /// </summary>
    /// <param name="filePath">Destination file path</param>
    /// <param name="includeEvents">Whether to include event markers</param>
    /// <param name="progress">Progress callback (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ExportResult> ExportToKmlAsync(
        string filePath,
        bool includeEvents = true,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports the current graph as PNG image.
    /// </summary>
    Task<ExportResult> ExportGraphToPngAsync(
        string filePath,
        int width = 1920,
        int height = 1080,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an export operation.
/// </summary>
public class ExportResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int RecordCount { get; set; }
    public TimeSpan ExportDuration { get; set; }
}

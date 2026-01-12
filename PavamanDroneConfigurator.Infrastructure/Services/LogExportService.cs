using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Infrastructure.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for exporting log data to CSV, KML, and other formats.
/// </summary>
public class LogExportService : ILogExportService
{
    private readonly ILogger<LogExportService> _logger;
    private readonly ILogQueryEngine _queryEngine;
    private readonly ILogEventDetector _eventDetector;
    private DataFlashLogParser? _parser;
    private ParsedLog? _parsedLog;

    public LogExportService(
        ILogger<LogExportService> logger,
        ILogQueryEngine queryEngine,
        ILogEventDetector eventDetector)
    {
        _logger = logger;
        _queryEngine = queryEngine;
        _eventDetector = eventDetector;
    }

    /// <summary>
    /// Sets the parsed log data for export.
    /// </summary>
    public void SetLogData(DataFlashLogParser parser, ParsedLog parsedLog)
    {
        _parser = parser;
        _parsedLog = parsedLog;
    }

    public async Task<ExportResult> ExportToCsvAsync(
        string filePath,
        IEnumerable<string> seriesKeys,
        double? startTime,
        double? endTime,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ExportResult { FilePath = filePath };

        try
        {
            progress?.Report(0);

            var keysList = seriesKeys.ToList();
            if (keysList.Count == 0)
            {
                result.ErrorMessage = "No series selected for export";
                return result;
            }

            // Get all series data
            progress?.Report(10);
            var seriesData = await _queryEngine.GetMultipleSeriesAsync(
                keysList, startTime, endTime, int.MaxValue, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                result.ErrorMessage = "Export cancelled";
                return result;
            }

            progress?.Report(30);

            // Build unified time index
            var allTimes = new SortedSet<double>();
            foreach (var series in seriesData.Values)
            {
                foreach (var t in series.Times)
                {
                    allTimes.Add(t);
                }
            }

            if (allTimes.Count == 0)
            {
                result.ErrorMessage = "No data to export";
                return result;
            }

            progress?.Report(50);

            // Write CSV
            await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Header
            await writer.WriteAsync("Time");
            foreach (var key in keysList)
            {
                await writer.WriteAsync($",{EscapeCsvField(key)}");
            }
            await writer.WriteLineAsync();

            // Build lookup dictionaries for each series
            var seriesLookups = new Dictionary<string, Dictionary<double, double>>();
            foreach (var key in keysList)
            {
                var lookup = new Dictionary<double, double>();
                if (seriesData.TryGetValue(key, out var series))
                {
                    for (int i = 0; i < series.Times.Length; i++)
                    {
                        lookup[series.Times[i]] = series.Values[i];
                    }
                }
                seriesLookups[key] = lookup;
            }

            // Write data rows
            var timesList = allTimes.ToList();
            var totalRows = timesList.Count;
            var rowsWritten = 0;

            foreach (var time in timesList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.ErrorMessage = "Export cancelled";
                    return result;
                }

                await writer.WriteAsync(time.ToString("F6", CultureInfo.InvariantCulture));

                foreach (var key in keysList)
                {
                    if (seriesLookups[key].TryGetValue(time, out var value))
                    {
                        await writer.WriteAsync($",{value.ToString("G", CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        await writer.WriteAsync(",");
                    }
                }
                await writer.WriteLineAsync();

                rowsWritten++;
                if (rowsWritten % 1000 == 0)
                {
                    var pct = 50 + (rowsWritten * 50 / totalRows);
                    progress?.Report(pct);
                }
            }

            await writer.FlushAsync();
            
            var fileInfo = new FileInfo(filePath);
            result.IsSuccess = true;
            result.FileSizeBytes = fileInfo.Length;
            result.RecordCount = rowsWritten;
            result.ExportDuration = sw.Elapsed;

            progress?.Report(100);

            _logger.LogInformation("Exported {Count} rows to CSV: {File}", rowsWritten, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to CSV");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<ExportResult> ExportToKmlAsync(
        string filePath,
        bool includeEvents = true,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ExportResult { FilePath = filePath };

        try
        {
            progress?.Report(0);

            if (_parser == null || _parsedLog == null)
            {
                result.ErrorMessage = "No log data loaded";
                return result;
            }

            // Get GPS data
            var latData = _parser.GetDataSeries("GPS", "Lat");
            var lngData = _parser.GetDataSeries("GPS", "Lng");
            var altData = _parser.GetDataSeries("GPS", "Alt");

            if (latData == null || lngData == null || latData.Count == 0)
            {
                result.ErrorMessage = "No GPS data in log";
                return result;
            }

            progress?.Report(20);

            // Get events if requested
            List<LogEvent>? events = null;
            if (includeEvents)
            {
                events = await _eventDetector.DetectEventsAsync(null, cancellationToken);
            }

            progress?.Report(40);

            // Build KML
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
            sb.AppendLine("  <Document>");
            sb.AppendLine($"    <name>{EscapeXml(_parsedLog.FileName)}</name>");
            sb.AppendLine($"    <description>Flight log from {_parsedLog.ParseTime:yyyy-MM-dd HH:mm:ss}</description>");

            // Styles
            sb.AppendLine("    <Style id=\"flightPath\">");
            sb.AppendLine("      <LineStyle>");
            sb.AppendLine("        <color>ff0000ff</color>");
            sb.AppendLine("        <width>3</width>");
            sb.AppendLine("      </LineStyle>");
            sb.AppendLine("    </Style>");

            sb.AppendLine("    <Style id=\"eventWarning\">");
            sb.AppendLine("      <IconStyle>");
            sb.AppendLine("        <color>ff00ffff</color>");
            sb.AppendLine("        <Icon><href>http://maps.google.com/mapfiles/kml/shapes/caution.png</href></Icon>");
            sb.AppendLine("      </IconStyle>");
            sb.AppendLine("    </Style>");

            sb.AppendLine("    <Style id=\"eventError\">");
            sb.AppendLine("      <IconStyle>");
            sb.AppendLine("        <color>ff0000ff</color>");
            sb.AppendLine("        <Icon><href>http://maps.google.com/mapfiles/kml/shapes/target.png</href></Icon>");
            sb.AppendLine("      </IconStyle>");
            sb.AppendLine("    </Style>");

            // Flight path
            sb.AppendLine("    <Placemark>");
            sb.AppendLine("      <name>Flight Path</name>");
            sb.AppendLine("      <styleUrl>#flightPath</styleUrl>");
            sb.AppendLine("      <LineString>");
            sb.AppendLine("        <altitudeMode>absolute</altitudeMode>");
            sb.AppendLine("        <coordinates>");

            var minCount = Math.Min(latData.Count, lngData.Count);
            var altCount = altData?.Count ?? 0;

            for (int i = 0; i < minCount; i++)
            {
                var lat = latData[i].Value;
                var lng = lngData[i].Value;
                var alt = i < altCount ? altData![i].Value : 0;

                // Skip invalid coordinates
                if (Math.Abs(lat) < 0.001 && Math.Abs(lng) < 0.001)
                    continue;

                sb.AppendLine($"          {lng.ToString("F7", CultureInfo.InvariantCulture)},{lat.ToString("F7", CultureInfo.InvariantCulture)},{alt.ToString("F1", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine("        </coordinates>");
            sb.AppendLine("      </LineString>");
            sb.AppendLine("    </Placemark>");

            progress?.Report(70);

            // Events folder
            if (events != null && events.Count > 0)
            {
                sb.AppendLine("    <Folder>");
                sb.AppendLine("      <name>Events</name>");

                foreach (var evt in events.Where(e => e.Severity >= LogEventSeverity.Warning))
                {
                    var (lat, lng, alt) = GetPositionAtTime(latData, lngData, altData, evt.Timestamp);
                    if (lat == 0 && lng == 0) continue;

                    var styleId = evt.Severity >= LogEventSeverity.Error ? "eventError" : "eventWarning";

                    sb.AppendLine("      <Placemark>");
                    sb.AppendLine($"        <name>{EscapeXml(evt.Title)}</name>");
                    sb.AppendLine($"        <description>{EscapeXml(evt.Description)}</description>");
                    sb.AppendLine($"        <styleUrl>#{styleId}</styleUrl>");
                    sb.AppendLine("        <Point>");
                    sb.AppendLine("          <altitudeMode>absolute</altitudeMode>");
                    sb.AppendLine($"          <coordinates>{lng.ToString("F7", CultureInfo.InvariantCulture)},{lat.ToString("F7", CultureInfo.InvariantCulture)},{alt.ToString("F1", CultureInfo.InvariantCulture)}</coordinates>");
                    sb.AppendLine("        </Point>");
                    sb.AppendLine("      </Placemark>");
                }

                sb.AppendLine("    </Folder>");
            }

            sb.AppendLine("  </Document>");
            sb.AppendLine("</kml>");

            progress?.Report(90);

            // Write file
            await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);

            var fileInfo = new FileInfo(filePath);
            result.IsSuccess = true;
            result.FileSizeBytes = fileInfo.Length;
            result.RecordCount = minCount;
            result.ExportDuration = sw.Elapsed;

            progress?.Report(100);

            _logger.LogInformation("Exported GPS track to KML: {File}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to KML");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public Task<ExportResult> ExportGraphToPngAsync(
        string filePath,
        int width = 1920,
        int height = 1080,
        CancellationToken cancellationToken = default)
    {
        // This would be implemented by the graph control directly
        // The service can provide the interface but actual rendering is done by the view
        return Task.FromResult(new ExportResult
        {
            FilePath = filePath,
            IsSuccess = false,
            ErrorMessage = "Graph export should be triggered from the view"
        });
    }

    private static (double Lat, double Lng, double Alt) GetPositionAtTime(
        List<LogDataPoint> latData,
        List<LogDataPoint> lngData,
        List<LogDataPoint>? altData,
        double timestamp)
    {
        // Convert timestamp to microseconds (log format)
        var timeUs = timestamp * 1e6;

        // Find nearest point
        var nearestIdx = 0;
        var minDiff = double.MaxValue;

        for (int i = 0; i < latData.Count; i++)
        {
            var diff = Math.Abs(latData[i].Timestamp - timeUs);
            if (diff < minDiff)
            {
                minDiff = diff;
                nearestIdx = i;
            }
        }

        if (nearestIdx < latData.Count && nearestIdx < lngData.Count)
        {
            var lat = latData[nearestIdx].Value;
            var lng = lngData[nearestIdx].Value;
            var alt = altData != null && nearestIdx < altData.Count ? altData[nearestIdx].Value : 0;
            return (lat, lng, alt);
        }

        return (0, 0, 0);
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}

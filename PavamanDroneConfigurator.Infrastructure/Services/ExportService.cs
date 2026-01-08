using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for exporting drone parameters to various file formats.
/// </summary>
public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;
    
    // Characters that are not allowed in Windows filenames
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ExportToStringAsync(IEnumerable<DroneParameter> parameters, ExportFileFormat format)
    {
        var paramList = parameters.ToList();
        
        return format switch
        {
            ExportFileFormat.Csv => await ExportToCsvAsync(paramList),
            ExportFileFormat.Params => await ExportToParamsAsync(paramList),
            ExportFileFormat.Cfg => await ExportToCfgAsync(paramList),
            ExportFileFormat.Json => await ExportToJsonAsync(paramList),
            ExportFileFormat.Yaml => await ExportToYamlAsync(paramList),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format")
        };
    }

    /// <inheritdoc />
    public async Task<bool> ExportToFileAsync(IEnumerable<DroneParameter> parameters, ExportFileFormat format, string filePath)
    {
        try
        {
            var content = await ExportToStringAsync(parameters, format);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Write file asynchronously
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            
            _logger.LogInformation("Successfully exported {Count} parameters to {FilePath}", 
                parameters.Count(), filePath);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export parameters to {FilePath}", filePath);
            return false;
        }
    }

    /// <inheritdoc />
    public string GetFileExtension(ExportFileFormat format)
    {
        return format switch
        {
            ExportFileFormat.Csv => ".csv",
            ExportFileFormat.Params => ".params",
            ExportFileFormat.Cfg => ".cfg",
            ExportFileFormat.Json => ".json",
            ExportFileFormat.Yaml => ".yaml",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format")
        };
    }

    /// <inheritdoc />
    public string GetMimeType(ExportFileFormat format)
    {
        return format switch
        {
            ExportFileFormat.Csv => "text/csv",
            ExportFileFormat.Params => "text/plain",
            ExportFileFormat.Cfg => "text/plain",
            ExportFileFormat.Json => "application/json",
            ExportFileFormat.Yaml => "application/x-yaml",
            _ => "application/octet-stream"
        };
    }

    /// <inheritdoc />
    public bool IsValidFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;
        
        return !filename.Any(c => InvalidFileNameChars.Contains(c));
    }

    /// <inheritdoc />
    public string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "drone_parameters";
        
        var sanitized = new StringBuilder();
        foreach (var c in filename)
        {
            if (!InvalidFileNameChars.Contains(c))
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }
        
        var result = sanitized.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "drone_parameters" : result;
    }

    #region Format-Specific Export Methods

    /// <summary>
    /// Exports parameters to CSV format: ParameterID,Value
    /// </summary>
    private Task<string> ExportToCsvAsync(List<DroneParameter> parameters)
    {
        var sb = new StringBuilder();
        
        // Header row
        sb.AppendLine("ParameterID,Value");
        
        // Data rows
        foreach (var param in parameters.OrderBy(p => p.Name))
        {
            // Escape values that contain commas or quotes
            var value = param.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"{param.Name},{value}");
        }
        
        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Exports parameters to ArduPilot .params format: PARAMETER_NAME VALUE
    /// </summary>
    private Task<string> ExportToParamsAsync(List<DroneParameter> parameters)
    {
        var sb = new StringBuilder();
        
        // Add header comment
        sb.AppendLine($"# ArduPilot Parameter File");
        sb.AppendLine($"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Parameters: {parameters.Count}");
        sb.AppendLine();
        
        foreach (var param in parameters.OrderBy(p => p.Name))
        {
            var value = param.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"{param.Name} {value}");
        }
        
        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Exports parameters to .cfg format: PARAMETER_NAME=VALUE
    /// </summary>
    private Task<string> ExportToCfgAsync(List<DroneParameter> parameters)
    {
        var sb = new StringBuilder();
        
        // Add header comment
        sb.AppendLine($"; Configuration File");
        sb.AppendLine($"; Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"; Parameters: {parameters.Count}");
        sb.AppendLine();
        
        foreach (var param in parameters.OrderBy(p => p.Name))
        {
            var value = param.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"{param.Name}={value}");
        }
        
        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Exports parameters to JSON format with full details.
    /// </summary>
    private Task<string> ExportToJsonAsync(List<DroneParameter> parameters)
    {
        var exportData = new
        {
            metadata = new
            {
                exportedAt = DateTime.Now.ToString("O"),
                parameterCount = parameters.Count,
                application = "Pavaman Drone Configurator"
            },
            parameters = parameters.OrderBy(p => p.Name).Select(p => new
            {
                name = p.Name,
                value = p.Value,
                description = p.Description,
                minValue = p.MinValue,
                maxValue = p.MaxValue
            }).ToList()
        };
        
        var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
        return Task.FromResult(json);
    }

    /// <summary>
    /// Exports parameters to YAML format with full details.
    /// </summary>
    private Task<string> ExportToYamlAsync(List<DroneParameter> parameters)
    {
        var exportData = new Dictionary<string, object>
        {
            ["metadata"] = new Dictionary<string, object>
            {
                ["exported_at"] = DateTime.Now.ToString("O"),
                ["parameter_count"] = parameters.Count,
                ["application"] = "Pavaman Drone Configurator"
            },
            ["parameters"] = parameters.OrderBy(p => p.Name).Select(p => new Dictionary<string, object?>
            {
                ["name"] = p.Name,
                ["value"] = p.Value,
                ["description"] = p.Description,
                ["min_value"] = p.MinValue,
                ["max_value"] = p.MaxValue
            }).ToList()
        };
        
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
        
        var yaml = serializer.Serialize(exportData);
        return Task.FromResult(yaml);
    }

    #endregion
}

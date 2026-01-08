using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for importing drone parameters from various file formats.
/// </summary>
public class ImportService : IImportService
{
    private readonly ILogger<ImportService> _logger;
    
    private static readonly Dictionary<string, ExportFileFormat> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = ExportFileFormat.Csv,
        [".params"] = ExportFileFormat.Params,
        [".cfg"] = ExportFileFormat.Cfg,
        [".json"] = ExportFileFormat.Json,
        [".yaml"] = ExportFileFormat.Yaml,
        [".yml"] = ExportFileFormat.Yaml
    };

    public ImportService(ILogger<ImportService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportFromFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new ImportResult { ErrorMessage = "File path is empty." };
            }

            if (!File.Exists(filePath))
            {
                return new ImportResult { ErrorMessage = $"File not found: {filePath}" };
            }

            var format = DetectFormat(filePath);
            if (format == null)
            {
                var ext = Path.GetExtension(filePath);
                return new ImportResult { ErrorMessage = $"Unsupported file format: {ext}" };
            }

            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ImportResult { ErrorMessage = "File is empty." };
            }

            _logger.LogInformation("Importing parameters from {FilePath} (format: {Format})", filePath, format);
            
            return await ImportFromStringAsync(content, format.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import parameters from {FilePath}", filePath);
            return new ImportResult { ErrorMessage = $"Import failed: {ex.Message}" };
        }
    }

    /// <inheritdoc />
    public Task<ImportResult> ImportFromStringAsync(string content, ExportFileFormat format)
    {
        try
        {
            var result = format switch
            {
                ExportFileFormat.Csv => ParseCsv(content),
                ExportFileFormat.Params => ParseParams(content),
                ExportFileFormat.Cfg => ParseCfg(content),
                ExportFileFormat.Json => ParseJson(content),
                ExportFileFormat.Yaml => ParseYaml(content),
                _ => new ImportResult { ErrorMessage = $"Unsupported format: {format}" }
            };

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully parsed {Count} parameters ({Duplicates} duplicates, {Skipped} skipped)",
                    result.SuccessCount, result.DuplicateCount, result.SkippedCount);
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse content as {Format}", format);
            return Task.FromResult(new ImportResult { ErrorMessage = $"Parse failed: {ex.Message}" });
        }
    }

    /// <inheritdoc />
    public ExportFileFormat? DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ExtensionMap.TryGetValue(ext, out var format) ? format : null;
    }

    /// <inheritdoc />
    public string[] GetSupportedExtensions()
    {
        return ExtensionMap.Keys.ToArray();
    }

    /// <inheritdoc />
    public string GetFileDialogFilter()
    {
        return "All Supported|*.csv;*.params;*.cfg;*.json;*.yaml;*.yml|" +
               "CSV Files|*.csv|" +
               "ArduPilot Parameters|*.params|" +
               "Configuration Files|*.cfg|" +
               "JSON Files|*.json|" +
               "YAML Files|*.yaml;*.yml";
    }

    #region Parsers

    /// <summary>
    /// Parses CSV format: ParameterID,Value (with optional header)
    /// </summary>
    private ImportResult ParseCsv(string content)
    {
        var parameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var duplicateCount = 0;
        var skippedCount = 0;

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var startIndex = 0;

        // Skip header row if present
        if (lines.Length > 0)
        {
            var firstLine = lines[0].Trim();
            if (firstLine.StartsWith("ParameterID", StringComparison.OrdinalIgnoreCase) ||
                firstLine.StartsWith("Name", StringComparison.OrdinalIgnoreCase) ||
                firstLine.StartsWith("Parameter", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
            }
        }

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 2)
            {
                skippedCount++;
                warnings.Add($"Line {i + 1}: Invalid format (expected 'Name,Value')");
                continue;
            }

            var name = parts[0].Trim();
            var valueStr = parts[1].Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                skippedCount++;
                continue;
            }

            if (!TryParseFloat(valueStr, out var value))
            {
                skippedCount++;
                warnings.Add($"Line {i + 1}: Invalid value '{valueStr}' for parameter '{name}'");
                continue;
            }

            if (parameters.ContainsKey(name))
            {
                duplicateCount++;
                warnings.Add($"Duplicate parameter '{name}' - using latest value ({value})");
            }

            parameters[name] = value;
        }

        return new ImportResult
        {
            Parameters = parameters,
            DuplicateCount = duplicateCount,
            SkippedCount = skippedCount,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Parses ArduPilot .params format: PARAMETER_NAME VALUE (space-separated)
    /// Ignores blank lines and lines starting with # (comments)
    /// </summary>
    private ImportResult ParseParams(string content)
    {
        var parameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var duplicateCount = 0;
        var skippedCount = 0;

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            // Split by whitespace (one or more spaces/tabs)
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                skippedCount++;
                warnings.Add($"Line {i + 1}: Invalid format (expected 'NAME VALUE')");
                continue;
            }

            var name = parts[0].Trim();
            var valueStr = parts[1].Trim();

            // Handle comma-separated values (some .params files use commas)
            if (valueStr.Contains(','))
            {
                valueStr = valueStr.Split(',')[0].Trim();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                skippedCount++;
                continue;
            }

            if (!TryParseFloat(valueStr, out var value))
            {
                skippedCount++;
                warnings.Add($"Line {i + 1}: Invalid value '{valueStr}' for parameter '{name}'");
                continue;
            }

            if (parameters.ContainsKey(name))
            {
                duplicateCount++;
                warnings.Add($"Duplicate parameter '{name}' - using latest value ({value})");
            }

            parameters[name] = value;
        }

        return new ImportResult
        {
            Parameters = parameters,
            DuplicateCount = duplicateCount,
            SkippedCount = skippedCount,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Parses .cfg format: PARAMETER_NAME=VALUE
    /// Ignores blank lines and lines starting with ; or # (comments)
    /// </summary>
    private ImportResult ParseCfg(string content)
    {
        var parameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var duplicateCount = 0;
        var skippedCount = 0;

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            // Skip section headers like [Section]
            if (line.StartsWith('[') && line.EndsWith(']'))
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0)
            {
                skippedCount++;
                warnings.Add($"Line {i + 1}: Invalid format (expected 'NAME=VALUE')");
                continue;
            }

            var name = line.Substring(0, eqIndex).Trim();
            var valueStr = line.Substring(eqIndex + 1).Trim();

            // Remove inline comments
            var commentIndex = valueStr.IndexOfAny(new[] { ';', '#' });
            if (commentIndex >= 0)
            {
                valueStr = valueStr.Substring(0, commentIndex).Trim();
            }

            // Remove surrounding quotes
            if ((valueStr.StartsWith('"') && valueStr.EndsWith('"')) ||
                (valueStr.StartsWith('\'') && valueStr.EndsWith('\'')))
            {
                valueStr = valueStr.Substring(1, valueStr.Length - 2);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                skippedCount++;
                continue;
            }

            if (!TryParseFloat(valueStr, out var value))
            {
                skippedCount++;
                warnings.Add($"Line {i + 1}: Invalid value '{valueStr}' for parameter '{name}'");
                continue;
            }

            if (parameters.ContainsKey(name))
            {
                duplicateCount++;
                warnings.Add($"Duplicate parameter '{name}' - using latest value ({value})");
            }

            parameters[name] = value;
        }

        return new ImportResult
        {
            Parameters = parameters,
            DuplicateCount = duplicateCount,
            SkippedCount = skippedCount,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Parses JSON format. Supports:
    /// - Simple object: { "PARAM1": 123, "PARAM2": 456 }
    /// - With metadata: { "parameters": [{ "name": "PARAM1", "value": 123 }, ...] }
    /// - Array of objects: [{ "name": "PARAM1", "value": 123 }, ...]
    /// </summary>
    private ImportResult ParseJson(string content)
    {
        var parameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var duplicateCount = 0;
        var skippedCount = 0;

        try
        {
            var json = JToken.Parse(content);

            if (json is JObject obj)
            {
                // Check for structured format with "parameters" array
                if (obj.TryGetValue("parameters", out var paramsToken) && paramsToken is JArray paramsArray)
                {
                    ParseJsonParameterArray(paramsArray, parameters, warnings, ref duplicateCount, ref skippedCount);
                }
                else
                {
                    // Simple key-value object format
                    foreach (var prop in obj.Properties())
                    {
                        // Skip metadata properties
                        if (prop.Name.Equals("metadata", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("exportedAt", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!TryParseJsonValue(prop.Value, out var value))
                        {
                            skippedCount++;
                            warnings.Add($"Invalid value for parameter '{prop.Name}'");
                            continue;
                        }

                        if (parameters.ContainsKey(prop.Name))
                        {
                            duplicateCount++;
                            warnings.Add($"Duplicate parameter '{prop.Name}' - using latest value ({value})");
                        }

                        parameters[prop.Name] = value;
                    }
                }
            }
            else if (json is JArray array)
            {
                ParseJsonParameterArray(array, parameters, warnings, ref duplicateCount, ref skippedCount);
            }
            else
            {
                return new ImportResult { ErrorMessage = "Invalid JSON structure. Expected object or array." };
            }
        }
        catch (JsonReaderException ex)
        {
            return new ImportResult { ErrorMessage = $"Invalid JSON: {ex.Message}" };
        }

        return new ImportResult
        {
            Parameters = parameters,
            DuplicateCount = duplicateCount,
            SkippedCount = skippedCount,
            Warnings = warnings
        };
    }

    private void ParseJsonParameterArray(JArray array, Dictionary<string, float> parameters, 
        List<string> warnings, ref int duplicateCount, ref int skippedCount)
    {
        foreach (var item in array)
        {
            if (item is not JObject paramObj)
            {
                skippedCount++;
                continue;
            }

            // Try different property names for name/value
            var name = paramObj.Value<string>("name") ?? 
                       paramObj.Value<string>("Name") ?? 
                       paramObj.Value<string>("id") ??
                       paramObj.Value<string>("ParameterID");
            
            var valueToken = paramObj["value"] ?? paramObj["Value"];

            if (string.IsNullOrWhiteSpace(name))
            {
                skippedCount++;
                warnings.Add("Parameter object missing 'name' property");
                continue;
            }

            if (valueToken == null || !TryParseJsonValue(valueToken, out var value))
            {
                skippedCount++;
                warnings.Add($"Invalid or missing value for parameter '{name}'");
                continue;
            }

            if (parameters.ContainsKey(name))
            {
                duplicateCount++;
                warnings.Add($"Duplicate parameter '{name}' - using latest value ({value})");
            }

            parameters[name] = value;
        }
    }

    private static bool TryParseJsonValue(JToken token, out float value)
    {
        value = 0;

        return token.Type switch
        {
            JTokenType.Integer => float.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value),
            JTokenType.Float => float.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value),
            JTokenType.String => TryParseFloat(token.Value<string>() ?? "", out value),
            _ => false
        };
    }

    /// <summary>
    /// Parses YAML format. Supports:
    /// - Simple key-value: PARAM1: 123
    /// - With metadata: { parameters: [{ name: PARAM1, value: 123 }, ...] }
    /// </summary>
    private ImportResult ParseYaml(string content)
    {
        var parameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var duplicateCount = 0;
        var skippedCount = 0;

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yaml = deserializer.Deserialize<Dictionary<string, object>>(content);

            if (yaml == null)
            {
                return new ImportResult { ErrorMessage = "Empty or invalid YAML file." };
            }

            // Check for structured format with "parameters" key
            if (yaml.TryGetValue("parameters", out var paramsObj) && paramsObj is List<object> paramsList)
            {
                foreach (var item in paramsList)
                {
                    if (item is not Dictionary<object, object> paramDict)
                    {
                        skippedCount++;
                        continue;
                    }

                    var name = GetYamlStringValue(paramDict, "name") ?? 
                               GetYamlStringValue(paramDict, "id");
                    var valueObj = GetYamlValue(paramDict, "value");

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (valueObj == null || !TryParseYamlValue(valueObj, out var value))
                    {
                        skippedCount++;
                        warnings.Add($"Invalid or missing value for parameter '{name}'");
                        continue;
                    }

                    if (parameters.ContainsKey(name))
                    {
                        duplicateCount++;
                        warnings.Add($"Duplicate parameter '{name}' - using latest value ({value})");
                    }

                    parameters[name] = value;
                }
            }
            else
            {
                // Simple key-value format at root level
                foreach (var kvp in yaml)
                {
                    // Skip metadata keys
                    if (kvp.Key.Equals("metadata", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("version", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!TryParseYamlValue(kvp.Value, out var value))
                    {
                        skippedCount++;
                        warnings.Add($"Invalid value for parameter '{kvp.Key}'");
                        continue;
                    }

                    if (parameters.ContainsKey(kvp.Key))
                    {
                        duplicateCount++;
                        warnings.Add($"Duplicate parameter '{kvp.Key}' - using latest value ({value})");
                    }

                    parameters[kvp.Key] = value;
                }
            }
        }
        catch (Exception ex)
        {
            return new ImportResult { ErrorMessage = $"Invalid YAML: {ex.Message}" };
        }

        return new ImportResult
        {
            Parameters = parameters,
            DuplicateCount = duplicateCount,
            SkippedCount = skippedCount,
            Warnings = warnings
        };
    }

    private static string? GetYamlStringValue(Dictionary<object, object> dict, string key)
    {
        foreach (var kvp in dict)
        {
            if (kvp.Key.ToString()?.Equals(key, StringComparison.OrdinalIgnoreCase) == true)
            {
                return kvp.Value?.ToString();
            }
        }
        return null;
    }

    private static object? GetYamlValue(Dictionary<object, object> dict, string key)
    {
        foreach (var kvp in dict)
        {
            if (kvp.Key.ToString()?.Equals(key, StringComparison.OrdinalIgnoreCase) == true)
            {
                return kvp.Value;
            }
        }
        return null;
    }

    private static bool TryParseYamlValue(object? obj, out float value)
    {
        value = 0;
        if (obj == null) return false;

        return obj switch
        {
            int i => (value = i) == i,
            long l => (value = l) == l,
            float f => (value = f) == f,
            double d => (value = (float)d) == (float)d,
            decimal dec => (value = (float)dec) == (float)dec,
            string s => TryParseFloat(s, out value),
            _ => TryParseFloat(obj.ToString() ?? "", out value)
        };
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Tries to parse a string as a float, handling various formats.
    /// </summary>
    private static bool TryParseFloat(string input, out float value)
    {
        value = 0;
        
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        // Handle scientific notation and various number formats
        return float.TryParse(input, 
            NumberStyles.Float | NumberStyles.AllowExponent | NumberStyles.AllowThousands, 
            CultureInfo.InvariantCulture, 
            out value);
    }

    #endregion
}

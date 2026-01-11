using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Parses ArduPilot parameter metadata XML files (*.pdef.xml format).
/// Converts official ArduPilot documentation into ParameterMetadata objects.
/// </summary>
public class ArduPilotXmlParser
{
    private readonly ILogger<ArduPilotXmlParser> _logger;

    public ArduPilotXmlParser(ILogger<ArduPilotXmlParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses ArduPilot parameter XML content into a dictionary of metadata.
    /// </summary>
    /// <param name="xmlContent">XML content from *.pdef.xml file</param>
    /// <returns>Dictionary of parameter name to metadata</returns>
    public Dictionary<string, ParameterMetadata> ParseXml(string xmlContent)
    {
        var parameters = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var paramCount = 0;

            foreach (var paramElement in doc.Descendants("param"))
            {
                try
                {
                    var metadata = ParseParameter(paramElement);
                    if (metadata != null)
                    {
                        parameters[metadata.Name] = metadata;
                        paramCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse parameter element");
                }
            }

            _logger.LogInformation("Parsed {Count} parameters from XML", paramCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse XML document");
        }

        return parameters;
    }

    private ParameterMetadata? ParseParameter(XElement paramElement)
    {
        var name = paramElement.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name))
        {
            _logger.LogWarning("Parameter element missing 'name' attribute");
            return null;
        }

        var metadata = new ParameterMetadata
        {
            Name = name,
            DisplayName = GetField(paramElement, "DisplayName") ?? 
                         paramElement.Attribute("humanName")?.Value ?? 
                         name,
            Description = GetField(paramElement, "Description") ?? 
                         paramElement.Attribute("documentation")?.Value ?? 
                         "No description available",
            Group = DetermineGroup(name),
            Units = GetField(paramElement, "Units") ?? GetField(paramElement, "UnitText")
        };

        // Parse range (format: "min max")
        ParseRange(paramElement, metadata);

        // Parse enum values (format: "0:Option1,1:Option2,2:Option3")
        ParseEnumValues(paramElement, metadata);

        // Parse increment (step size)
        var increment = GetField(paramElement, "Increment");
        if (!string.IsNullOrEmpty(increment) && float.TryParse(increment, NumberStyles.Float, CultureInfo.InvariantCulture, out var inc))
        {
            // Store in Range field for display purposes
            if (metadata.MinValue.HasValue && metadata.MaxValue.HasValue)
            {
                metadata.Range = $"{metadata.MinValue} - {metadata.MaxValue} (step: {inc})";
            }
        }
        else if (metadata.MinValue.HasValue && metadata.MaxValue.HasValue)
        {
            metadata.Range = $"{metadata.MinValue} - {metadata.MaxValue}";
        }

        return metadata;
    }

    private void ParseRange(XElement paramElement, ParameterMetadata metadata)
    {
        var range = GetField(paramElement, "Range");
        if (string.IsNullOrEmpty(range))
            return;

        var parts = range.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var min))
                metadata.MinValue = min;

            if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
                metadata.MaxValue = max;
        }
    }

    private void ParseEnumValues(XElement paramElement, ParameterMetadata metadata)
    {
        var values = GetField(paramElement, "Values");
        if (string.IsNullOrEmpty(values))
            return;

        var enumDict = new Dictionary<int, string>();

        // Format: "0:Disabled,1:Enabled" or "0:Stabilize, 1:Acro, 2:AltHold"
        var pairs = values.Split(',');
        foreach (var pair in pairs)
        {
            var parts = pair.Split(':');
            if (parts.Length == 2)
            {
                var keyStr = parts[0].Trim();
                var valueStr = parts[1].Trim();

                if (int.TryParse(keyStr, out var key))
                {
                    enumDict[key] = valueStr;
                }
            }
        }

        if (enumDict.Count > 0)
        {
            metadata.Values = enumDict;
        }
    }

    private string? GetField(XElement paramElement, string fieldName)
    {
        return paramElement.Elements("field")
            .FirstOrDefault(f => string.Equals(f.Attribute("name")?.Value, fieldName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private string DetermineGroup(string paramName)
    {
        // Extract prefix before first underscore (e.g., "ATC_RAT_PIT_P" -> "ATC")
        var underscoreIndex = paramName.IndexOf('_');
        if (underscoreIndex > 0)
        {
            var prefix = paramName.Substring(0, underscoreIndex);
            
            // Map common prefixes to friendly names
            return prefix switch
            {
                "ATC" => "Attitude Control",
                "PSC" => "Position Control",
                "WPNAV" => "Waypoint Navigation",
                "INS" => "Inertial Navigation",
                "EK3" => "Extended Kalman Filter",
                "BATT" => "Battery",
                "MOT" => "Motors",
                "SERVO" => "Servos",
                "RC" => "RC Channels",
                "RCMAP" => "RC Mapping",
                "RTL" => "Return to Launch",
                "LOIT" => "Loiter",
                "PHLD" => "Position Hold",
                "FHLD" => "Flow Hold",
                "AHRS" => "AHRS",
                "COMPASS" => "Compass",
                "GPS" => "GPS",
                "BARO" => "Barometer",
                "FENCE" => "Geofence",
                "RALLY" => "Rally Points",
                "LOG" => "Logging",
                "SR" => "Telemetry Rates",
                "SERIAL" => "Serial Ports",
                "ARMING" => "Arming",
                "FS" => "Failsafe",
                "FLTMODE" => "Flight Modes",
                "FRAME" => "Frame",
                "ACRO" => "Acro Mode",
                "AUTO" => "Auto Mode",
                "GUID" => "Guided Mode",
                "AVOID" => "Avoidance",
                "PLND" => "Precision Landing",
                "CAM" => "Camera",
                "MNT" => "Gimbal",
                "SPRAY" => "Sprayer",
                "TERRAIN" => "Terrain",
                _ => prefix // Use prefix as-is
            };
        }

        return "General";
    }
}

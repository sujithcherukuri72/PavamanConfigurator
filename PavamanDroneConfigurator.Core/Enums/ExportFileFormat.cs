namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Supported file formats for parameter export.
/// </summary>
public enum ExportFileFormat
{
    /// <summary>
    /// CSV format: ParameterID,Value
    /// </summary>
    Csv,
    
    /// <summary>
    /// ArduPilot .params format: PARAMETER_NAME VALUE
    /// </summary>
    Params,
    
    /// <summary>
    /// Configuration file format: PARAMETER_NAME=VALUE
    /// </summary>
    Cfg,
    
    /// <summary>
    /// JSON format with full parameter details
    /// </summary>
    Json,
    
    /// <summary>
    /// YAML format with full parameter details
    /// </summary>
    Yaml
}

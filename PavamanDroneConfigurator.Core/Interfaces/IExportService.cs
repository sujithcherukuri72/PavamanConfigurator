using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service for exporting drone parameters to various file formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports parameters to the specified format and returns the content as a string.
    /// </summary>
    /// <param name="parameters">The parameters to export.</param>
    /// <param name="format">The target file format.</param>
    /// <returns>The formatted content string.</returns>
    Task<string> ExportToStringAsync(IEnumerable<DroneParameter> parameters, ExportFileFormat format);
    
    /// <summary>
    /// Exports parameters to a file at the specified path.
    /// </summary>
    /// <param name="parameters">The parameters to export.</param>
    /// <param name="format">The target file format.</param>
    /// <param name="filePath">The full path where the file will be saved.</param>
    /// <returns>True if export was successful, false otherwise.</returns>
    Task<bool> ExportToFileAsync(IEnumerable<DroneParameter> parameters, ExportFileFormat format, string filePath);
    
    /// <summary>
    /// Gets the file extension for the specified format (includes the dot).
    /// </summary>
    /// <param name="format">The file format.</param>
    /// <returns>The file extension (e.g., ".csv").</returns>
    string GetFileExtension(ExportFileFormat format);
    
    /// <summary>
    /// Gets the MIME type for the specified format.
    /// </summary>
    /// <param name="format">The file format.</param>
    /// <returns>The MIME type string.</returns>
    string GetMimeType(ExportFileFormat format);
    
    /// <summary>
    /// Validates a filename for illegal characters.
    /// </summary>
    /// <param name="filename">The filename to validate (without extension).</param>
    /// <returns>True if the filename is valid, false otherwise.</returns>
    bool IsValidFilename(string filename);
    
    /// <summary>
    /// Sanitizes a filename by removing illegal characters.
    /// </summary>
    /// <param name="filename">The filename to sanitize.</param>
    /// <returns>A sanitized filename.</returns>
    string SanitizeFilename(string filename);
}

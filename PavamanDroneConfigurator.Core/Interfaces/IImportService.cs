using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Result of an import operation containing parsed parameters and any warnings.
/// </summary>
public record ImportResult
{
    /// <summary>
    /// Dictionary of successfully parsed parameters (Name -> Value).
    /// </summary>
    public Dictionary<string, float> Parameters { get; init; } = new();
    
    /// <summary>
    /// Number of parameters successfully parsed.
    /// </summary>
    public int SuccessCount => Parameters.Count;
    
    /// <summary>
    /// Number of duplicate keys encountered (last value wins).
    /// </summary>
    public int DuplicateCount { get; init; }
    
    /// <summary>
    /// Number of invalid/skipped rows.
    /// </summary>
    public int SkippedCount { get; init; }
    
    /// <summary>
    /// List of warning messages (e.g., duplicate keys, invalid rows).
    /// </summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>
    /// Whether the import completed successfully (at least some parameters parsed).
    /// </summary>
    public bool IsSuccess => Parameters.Count > 0;
    
    /// <summary>
    /// Error message if import failed completely.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Service for importing drone parameters from various file formats.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Imports parameters from a file at the specified path.
    /// </summary>
    /// <param name="filePath">The full path to the file to import.</param>
    /// <returns>Import result containing parsed parameters and any warnings.</returns>
    Task<ImportResult> ImportFromFileAsync(string filePath);
    
    /// <summary>
    /// Imports parameters from file content string.
    /// </summary>
    /// <param name="content">The file content as a string.</param>
    /// <param name="format">The file format to parse.</param>
    /// <returns>Import result containing parsed parameters and any warnings.</returns>
    Task<ImportResult> ImportFromStringAsync(string content, ExportFileFormat format);
    
    /// <summary>
    /// Detects the file format from the file extension.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>The detected format, or null if not supported.</returns>
    ExportFileFormat? DetectFormat(string filePath);
    
    /// <summary>
    /// Gets the list of supported file extensions for import.
    /// </summary>
    /// <returns>Array of supported extensions (e.g., ".csv", ".params").</returns>
    string[] GetSupportedExtensions();
    
    /// <summary>
    /// Gets a filter string for file dialogs (e.g., "CSV Files|*.csv|...").
    /// </summary>
    /// <returns>File filter string for open file dialogs.</returns>
    string GetFileDialogFilter();
}

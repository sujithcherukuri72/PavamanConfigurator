using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using System.Collections.ObjectModel;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for managing parameter metadata and enriching parameters with metadata information.
/// Follows MVVM architecture with separation of concerns:
/// - Repository handles data storage
/// - Service handles business logic
/// - ViewModels handle presentation logic
/// </summary>
public class ParameterMetadataService : IParameterMetadataService
{
    private readonly ILogger<ParameterMetadataService> _logger;
    private readonly IParameterMetadataRepository _repository;

    public ParameterMetadataService(
        ILogger<ParameterMetadataService> logger,
        IParameterMetadataRepository repository)
    {
        _logger = logger;
        _repository = repository;
        
        _logger.LogInformation("ParameterMetadataService initialized with {Count} parameters", _repository.GetCount());
    }

    public ParameterMetadata? GetMetadata(string parameterName)
    {
        try
        {
            return _repository.GetByName(parameterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata for parameter {ParameterName}", parameterName);
            return null;
        }
    }

    public IEnumerable<ParameterMetadata> GetAllMetadata()
    {
        try
        {
            return _repository.GetAll();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all metadata");
            return Enumerable.Empty<ParameterMetadata>();
        }
    }

    public IEnumerable<ParameterMetadata> GetParametersByGroup(string group)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                _logger.LogWarning("GetParametersByGroup called with empty group name");
                return Enumerable.Empty<ParameterMetadata>();
            }

            return _repository.GetByGroup(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parameters for group {Group}", group);
            return Enumerable.Empty<ParameterMetadata>();
        }
    }

    public IEnumerable<string> GetGroups()
    {
        try
        {
            return _repository.GetAllGroups();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parameter groups");
            return Enumerable.Empty<string>();
        }
    }

    public void EnrichParameter(DroneParameter parameter)
    {
        if (parameter == null)
        {
            _logger.LogWarning("EnrichParameter called with null parameter");
            return;
        }

        try
        {
            var meta = _repository.GetByName(parameter.Name);
            if (meta == null)
            {
                _logger.LogDebug("No metadata found for parameter {ParameterName}", parameter.Name);
                return;
            }

            // Enrich parameter with metadata
            parameter.Description = meta.Description;
            parameter.MinValue = meta.MinValue;
            parameter.MaxValue = meta.MaxValue;
            parameter.DefaultValue = meta.DefaultValue;
            parameter.Units = meta.Units;

            // Set up options for enum-type parameters
            if (meta.Values != null && meta.Values.Count > 0)
            {
                parameter.Options = new ObservableCollection<ParameterOption>(
                    meta.Values
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => new ParameterOption 
                        { 
                            Value = kvp.Key, 
                            Label = kvp.Value 
                        }));

                _logger.LogDebug("Enriched parameter {ParameterName} with {OptionCount} options", 
                    parameter.Name, parameter.Options.Count);
            }
            else
            {
                _logger.LogDebug("Enriched parameter {ParameterName} with range {Min}-{Max}", 
                    parameter.Name, meta.MinValue, meta.MaxValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching parameter {ParameterName}", parameter.Name);
        }
    }

    /// <summary>
    /// Validates a parameter value against its metadata constraints.
    /// Business logic method for parameter validation.
    /// </summary>
    public bool ValidateParameterValue(string parameterName, float value, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            var meta = _repository.GetByName(parameterName);
            if (meta == null)
            {
                // No metadata means no validation rules - allow the value
                return true;
            }

            // Check min/max bounds
            if (meta.MinValue.HasValue && value < meta.MinValue.Value)
            {
                errorMessage = $"Value {value} is below minimum {meta.MinValue.Value}";
                _logger.LogWarning("Validation failed for {Parameter}: {Error}", parameterName, errorMessage);
                return false;
            }

            if (meta.MaxValue.HasValue && value > meta.MaxValue.Value)
            {
                errorMessage = $"Value {value} exceeds maximum {meta.MaxValue.Value}";
                _logger.LogWarning("Validation failed for {Parameter}: {Error}", parameterName, errorMessage);
                return false;
            }

            // Check if value matches one of the enum options (for enum parameters)
            if (meta.Values != null && meta.Values.Count > 0)
            {
                int intValue = (int)Math.Round(value);
                if (!meta.Values.ContainsKey(intValue))
                {
                    errorMessage = $"Value {value} is not a valid option. Valid options: {string.Join(", ", meta.Values.Keys)}";
                    _logger.LogWarning("Validation failed for {Parameter}: {Error}", parameterName, errorMessage);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating parameter {ParameterName}", parameterName);
            errorMessage = "Validation error occurred";
            return false;
        }
    }

    /// <summary>
    /// Gets a user-friendly description for a parameter value.
    /// Business logic for value formatting and display.
    /// </summary>
    public string GetValueDescription(string parameterName, float value)
    {
        try
        {
            var meta = _repository.GetByName(parameterName);
            if (meta == null) return value.ToString("G");

            // For enum parameters, return the label
            if (meta.Values != null && meta.Values.Count > 0)
            {
                int intValue = (int)Math.Round(value);
                if (meta.Values.TryGetValue(intValue, out string? label))
                {
                    return $"{value} ({label})";
                }
            }

            // For numeric parameters, include units if available
            if (!string.IsNullOrEmpty(meta.Units))
            {
                return $"{value:G} {meta.Units}";
            }

            return value.ToString("G");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value description for {ParameterName}", parameterName);
            return value.ToString("G");
        }
    }

    /// <summary>
    /// Checks if metadata exists for a parameter.
    /// </summary>
    public bool HasMetadata(string parameterName)
    {
        try
        {
            return _repository.Exists(parameterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking metadata existence for {ParameterName}", parameterName);
            return false;
        }
    }

    /// <summary>
    /// Gets statistics about the metadata repository.
    /// </summary>
    public ParameterMetadataStatistics GetStatistics()
    {
        try
        {
            var allMetadata = _repository.GetAll().ToList();
            var groups = _repository.GetAllGroups().ToList();

            return new ParameterMetadataStatistics
            {
                TotalParameters = allMetadata.Count,
                ParametersWithOptions = allMetadata.Count(m => m.Values != null && m.Values.Count > 0),
                ParametersWithRanges = allMetadata.Count(m => m.MinValue.HasValue || m.MaxValue.HasValue),
                TotalGroups = groups.Count,
                GroupNames = groups
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata statistics");
            return new ParameterMetadataStatistics();
        }
    }
}
